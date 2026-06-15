using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class HrTimelineMessage
{
    public string type;
    public string mode;
    public string source;
    public string device;

    public string sessionId;

    public int sampleIndex;
    public int sampleCount;

    public float bpm;

    public string measuredAt;
    public string startedAt;
    public string endedAt;
    public string sentAt;
}

public class WorkoutHeartRateChartReceiver : MonoBehaviour
{
    public enum ChartUiMode
    {
        Simulation,
        Baseline
    }

    [Header("Network")]
    public int unityListenPort = 5055;

    [Header("Fallback (when watch timeline is unavailable)")]
    [Tooltip("Simulated/live HR from MockPhysiologySource — used when no UDP timeline on unityListenPort.")]
    public MockPhysiologySource physiologyFallback;
    public bool usePhysiologyFallbackWhenIdle = true;
    public float fallbackSampleIntervalSeconds = 1f;
    public float watchTimelineIdleSeconds = 5f;

    [Header("Designer UI (assign your Canvas panel)")]
    [Tooltip("WatchHrChart_Panel root — shown/hidden by TrainingFlowController during Sim 1/2.")]
    public GameObject chartPanelRoot;
    [Tooltip("RawImage inside the panel where the HR line chart is drawn.")]
    public RawImage chartImage;
    [Tooltip("ChartTitle TMP — assign for automatic title text.")]
    public TextMeshProUGUI titleTextTmp;
    [Tooltip("Title shown on ChartTitle when Update Title At Runtime is on.")]
    public string chartTitleText = "heart rate";
    [Tooltip("ChartInfoText TMP — assign for automatic status updates.")]
    public TextMeshProUGUI infoTextTmp;

    [Header("Baseline calibration UI (Baseline_Panel / Info)")]
    [Tooltip("RawImage on Baseline_Panel during sync/calibration.")]
    public RawImage baselineChartImage;
    public TextMeshProUGUI baselineTitleTextTmp;
    public TextMeshProUGUI baselineInfoTextTmp;
    public string baselineChartTitleText = "Heart rate";

    [Header("Info text (ChartInfoText)")]
    [Tooltip("Overwrite ChartInfoText at runtime (layout/style stay manual).")]
    public bool updateInfoAtRuntime = true;
    public float infoRefreshInterval = 0.5f;
    [TextArea] public string noWatchConnectedText = "No smartwatch connected";
    [TextArea] public string simulatedHrInfoText = "No smartwatch connected — simulated HR: {0:0} bpm";
    [TextArea] public string watchReceivingText = "Smartwatch connected — receiving data...";
    [TextArea] public string watchConnectedStatsText =
        "Smartwatch connected | Samples: {0} | HR: {1:0} bpm (avg {2:0.0})";

    [Header("Manual design")]
    [Tooltip("On = script only updates Chart Image texture. Design all labels/layout on WatchHrChart_Panel yourself.")]
    public bool manualDesignMode = true;
    [Tooltip("When Manual Design Mode is on, chart texture size follows ChartGraph RectTransform (WYSIWYG in editor).")]
    public bool useChartImageRectSize = true;
    [Tooltip("Overwrite ChartTitle TMP at runtime (layout/style stay manual).")]
    public bool updateTitleAtRuntime = true;

    [Header("Chart render")]
    public int chartWidth = 900;
    public int chartHeight = 420;
    [Tooltip("How often to redraw the chart while HR samples are arriving.")]
    public float chartRefreshInterval = 0.25f;
    [Tooltip("When using a designed panel background, keep the plot area transparent.")]
    public bool useTransparentChartBackground = true;
    public Color lineColor = new Color(0.3f, 0.75f, 0.95f, 1f);
    public int chartLineWidth = 3;
    public Color gridColor = new Color(0.42f, 0.5f, 0.58f, 0.45f);
    public Color axisColor = new Color(0.75f, 0.82f, 0.9f, 0.9f);
    public Color pointColor = Color.white;

    [Header("Runtime fallback (only if chartImage is not assigned)")]
    public bool createChartInFrontOfCamera = true;
    public float distanceFromCamera = 2f;
    public float verticalOffset = -0.15f;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;

    private readonly ConcurrentQueue<string> packetQueue = new ConcurrentQueue<string>();
    private readonly Dictionary<string, HrSessionData> sessions = new Dictionary<string, HrSessionData>();

    private string currentSessionId;
    private float lastPacketUnityTime;
    private bool chartDirty;

    private Canvas runtimeCanvas;
    private Text runtimeTitleText;
    private Text runtimeInfoText;

    private Texture2D chartTexture;
    private bool usingDesignerPanel;
    private float nextChartRefreshTime;
    private float nextFallbackSampleTime;
    private float nextInfoRefreshTime;
    private string lastDisplayedInfo;
    private Coroutine layoutRefreshRoutine;
    private ChartUiMode chartUiMode = ChartUiMode.Simulation;
    private const string FallbackSessionId = "simulated-hr";

    private class HrSample
    {
        public float bpm;
        public DateTimeOffset measuredAt;
        public string source;
        public int sampleIndex;
    }

    private class HrSessionData
    {
        public string sessionId;
        public int expectedSampleCount;
        public string startedAtRaw;
        public string endedAtRaw;

        public readonly List<HrSample> samples = new List<HrSample>();
        public readonly HashSet<string> sampleKeys = new HashSet<string>();
    }

    void Start()
    {
        Application.runInBackground = true;

        if (physiologyFallback == null)
            physiologyFallback = FindFirstObjectByType<MockPhysiologySource>();

        InitializeChartUi();
        StartUdpReceiver();

        ApplyChartTitle();
        ScheduleChartLayoutRefresh();
        RefreshInfoDisplay(true);
    }

    void Update()
    {
        int processed = 0;

        while (processed < 1000 && packetQueue.TryDequeue(out string json))
        {
            processed++;
            HandleJson(json);
        }

        TryAppendPhysiologyFallbackSample();
        RefreshInfoDisplay();

        if (chartDirty && Time.realtimeSinceStartup >= nextChartRefreshTime)
        {
            RenderCurrentSession();
            nextChartRefreshTime = Time.realtimeSinceStartup + chartRefreshInterval;
        }
    }

    void LateUpdate()
    {
        if (!usingDesignerPanel)
            UpdateRuntimeChartTransform();
    }

    public void SetChartUiMode(ChartUiMode mode)
    {
        if (chartUiMode == mode)
            return;

        chartUiMode = mode;
        chartDirty = true;
        nextInfoRefreshTime = 0f;
        ScheduleChartLayoutRefresh();
        RefreshInfoDisplay(true);
    }

    RawImage ActiveChartImage =>
        chartUiMode == ChartUiMode.Baseline && baselineChartImage != null
            ? baselineChartImage
            : chartImage;

    TextMeshProUGUI ActiveTitleTextTmp =>
        chartUiMode == ChartUiMode.Baseline && baselineTitleTextTmp != null
            ? baselineTitleTextTmp
            : titleTextTmp;

    TextMeshProUGUI ActiveInfoTextTmp =>
        chartUiMode == ChartUiMode.Baseline && baselineInfoTextTmp != null
            ? baselineInfoTextTmp
            : infoTextTmp;

    string ActiveChartTitleText =>
        chartUiMode == ChartUiMode.Baseline ? baselineChartTitleText : chartTitleText;

    private void InitializeChartUi()
    {
        usingDesignerPanel = chartImage != null || baselineChartImage != null;

        if (usingDesignerPanel)
            return;

        CreateRuntimeChartUi();
    }

    private void StartUdpReceiver()
    {
        if (running)
            return;

        udpClient = new UdpClient(unityListenPort);
        running = true;

        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log($"WorkoutHeartRateChartReceiver listening on UDP {unityListenPort}");
    }

    private void ReceiveLoop()
    {
        var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(data).Trim();

                if (!string.IsNullOrWhiteSpace(json))
                    packetQueue.Enqueue(json);
            }
            catch
            {
                // Expected when socket closes during shutdown.
            }
        }
    }

    private void HandleJson(string json)
    {
        HrTimelineMessage message;

        try
        {
            message = JsonUtility.FromJson<HrTimelineMessage>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not parse HR JSON: {e.Message}\n{json}");
            return;
        }

        if (message == null || string.IsNullOrWhiteSpace(message.type))
            return;

        lastPacketUnityTime = Time.realtimeSinceStartup;
        nextInfoRefreshTime = 0f;

        switch (message.type)
        {
            case "hr_session_start":
                HandleSessionStart(message);
                break;
            case "hr":
                HandleHrSample(message);
                break;
            case "hr_session_end":
                HandleSessionEnd(message);
                break;
        }
    }

    private void TryAppendPhysiologyFallbackSample()
    {
        if (!usePhysiologyFallbackWhenIdle || physiologyFallback == null)
            return;

        bool watchTimelineActive = lastPacketUnityTime > 0f &&
            Time.realtimeSinceStartup - lastPacketUnityTime < watchTimelineIdleSeconds;
        if (watchTimelineActive)
            return;

        if (Time.realtimeSinceStartup < nextFallbackSampleTime)
            return;

        nextFallbackSampleTime = Time.realtimeSinceStartup + fallbackSampleIntervalSeconds;

        float bpm = physiologyFallback.CurrentHeartRate;
        if (bpm <= 0f)
            return;

        currentSessionId = FallbackSessionId;
        var session = GetOrCreateSession(FallbackSessionId);

        DateTimeOffset measuredAt = DateTimeOffset.UtcNow;
        string key = $"{measuredAt.ToUnixTimeMilliseconds()}|{bpm:0.0}";
        if (session.sampleKeys.Contains(key))
            return;

        session.sampleKeys.Add(key);
        session.samples.Add(new HrSample
        {
            bpm = bpm,
            measuredAt = measuredAt,
            source = "simulated",
            sampleIndex = session.samples.Count
        });

        chartDirty = true;
        nextChartRefreshTime = 0f;
        nextInfoRefreshTime = 0f;
    }

    private void HandleSessionStart(HrTimelineMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.sessionId))
            return;

        currentSessionId = message.sessionId;

        var session = GetOrCreateSession(message.sessionId);
        session.expectedSampleCount = message.sampleCount;
        session.startedAtRaw = message.startedAt;
        session.endedAtRaw = message.endedAt;
        session.samples.Clear();
        session.sampleKeys.Clear();

        chartDirty = true;
        nextChartRefreshTime = 0f;
        nextInfoRefreshTime = 0f;

        Debug.Log($"HR session started: {message.sessionId}, expected samples: {message.sampleCount}");
    }

    private void HandleHrSample(HrTimelineMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.sessionId) || message.bpm <= 0f)
            return;

        if (!DateTimeOffset.TryParse(message.measuredAt, out DateTimeOffset measuredAt))
        {
            Debug.LogWarning($"Invalid measuredAt time: {message.measuredAt}");
            return;
        }

        var session = GetOrCreateSession(message.sessionId);
        string key = $"{measuredAt.ToUnixTimeMilliseconds()}|{message.bpm:0.0}";

        if (session.sampleKeys.Contains(key))
            return;

        session.sampleKeys.Add(key);
        session.samples.Add(new HrSample
        {
            bpm = message.bpm,
            measuredAt = measuredAt,
            source = message.source,
            sampleIndex = message.sampleIndex
        });

        currentSessionId = message.sessionId;
        chartDirty = true;
        nextChartRefreshTime = 0f;

        if (session.samples.Count % 25 == 0)
            nextInfoRefreshTime = 0f;
    }

    private void HandleSessionEnd(HrTimelineMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.sessionId))
            currentSessionId = message.sessionId;

        RenderCurrentSession();
        chartDirty = false;

        Debug.Log($"HR session ended: {message.sessionId}");
    }

    private HrSessionData GetOrCreateSession(string sessionId)
    {
        if (!sessions.TryGetValue(sessionId, out HrSessionData session))
        {
            session = new HrSessionData { sessionId = sessionId };
            sessions[sessionId] = session;
        }

        return session;
    }

    private void RenderCurrentSession()
    {
        if (string.IsNullOrWhiteSpace(currentSessionId))
            return;

        if (!sessions.TryGetValue(currentSessionId, out HrSessionData session))
            return;

        RenderSession(session);
    }

    private void RenderSession(HrSessionData session)
    {
        RawImage targetChart = ActiveChartImage;
        if (targetChart == null)
            return;

        List<HrSample> samples = session.samples.OrderBy(s => s.measuredAt).ToList();

        if (samples.Count < 2)
        {
            DrawEmptyChart();
            nextInfoRefreshTime = 0f;
            return;
        }

        DateTimeOffset start = samples.First().measuredAt;
        DateTimeOffset end = samples.Last().measuredAt;
        double durationSeconds = Math.Max(1.0, (end - start).TotalSeconds);

        float minBpm = samples.Min(s => s.bpm);
        float maxBpm = samples.Max(s => s.bpm);

        float yMin = minBpm;
        float yMax = maxBpm;
        float range = yMax - yMin;
        float padding = Mathf.Max(5f, range * 0.15f);
        yMin = Mathf.Max(0f, yMin - padding);
        yMax = yMax + padding;

        if (Mathf.Approximately(yMin, yMax))
        {
            yMin -= 5f;
            yMax += 5f;
        }

        GetChartDimensions(out int renderWidth, out int renderHeight);

        chartTexture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGBA32, false);
        chartTexture.filterMode = FilterMode.Bilinear;

        Color32 background = useTransparentChartBackground
            ? new Color32(0, 0, 0, 0)
            : new Color32(18, 18, 18, 255);
        Color32 grid = gridColor;
        Color32 axis = axisColor;
        Color32 line = lineColor;
        Color32 point = pointColor;

        FillTexture(chartTexture, background);

        int left = 60;
        int right = 20;
        int top = 20;
        int bottom = 50;
        int plotLeft = left;
        int plotRight = renderWidth - right;
        int plotBottom = bottom;
        int plotTop = renderHeight - top;
        int plotWidth = plotRight - plotLeft;
        int plotHeight = plotTop - plotBottom;

        for (int i = 0; i <= 5; i++)
        {
            int x = plotLeft + Mathf.RoundToInt(plotWidth * (i / 5f));
            DrawLine(chartTexture, x, plotBottom, x, plotTop, grid);
        }

        for (int i = 0; i <= 5; i++)
        {
            int y = plotBottom + Mathf.RoundToInt(plotHeight * (i / 5f));
            DrawLine(chartTexture, plotLeft, y, plotRight, y, grid);
        }

        DrawLine(chartTexture, plotLeft, plotBottom, plotRight, plotBottom, axis);
        DrawLine(chartTexture, plotLeft, plotBottom, plotLeft, plotTop, axis);

        Vector2Int? previous = null;

        foreach (HrSample sample in samples)
        {
            double secondsFromStart = (sample.measuredAt - start).TotalSeconds;
            float xNorm = Mathf.Clamp01((float)(secondsFromStart / durationSeconds));
            float yNorm = Mathf.Clamp01((sample.bpm - yMin) / (yMax - yMin));

            int x = plotLeft + Mathf.RoundToInt(xNorm * plotWidth);
            int y = plotBottom + Mathf.RoundToInt(yNorm * plotHeight);
            var current = new Vector2Int(x, y);

            if (previous.HasValue)
                DrawThickLine(chartTexture, previous.Value.x, previous.Value.y, current.x, current.y, line, chartLineWidth);

            DrawSmallPoint(chartTexture, x, y, point);
            previous = current;
        }

        chartTexture.Apply();
        targetChart.texture = chartTexture;

        ApplyChartTitle();
        nextInfoRefreshTime = 0f;
    }

    private void CreateRuntimeChartUi()
    {
        GameObject canvasObject = new GameObject("Workout HR Chart Canvas");
        canvasObject.transform.SetParent(transform, false);

        runtimeCanvas = canvasObject.AddComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.WorldSpace;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1000, 620);
        canvasObject.transform.localScale = Vector3.one * 0.0018f;

        UpdateRuntimeChartTransform(canvasObject.transform);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(canvasObject.transform, false);
        runtimeTitleText = titleObject.AddComponent<Text>();
        runtimeTitleText.font = font;
        runtimeTitleText.fontSize = 34;
        runtimeTitleText.alignment = TextAnchor.MiddleCenter;
        runtimeTitleText.color = Color.white;

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(950, 60);
        titleRect.anchoredPosition = new Vector2(0, 270);

        GameObject chartObject = new GameObject("Chart Image");
        chartObject.transform.SetParent(canvasObject.transform, false);
        chartImage = chartObject.AddComponent<RawImage>();

        RectTransform chartRect = chartObject.GetComponent<RectTransform>();
        chartRect.sizeDelta = new Vector2(chartWidth, chartHeight);
        chartRect.anchoredPosition = new Vector2(0, 15);

        GameObject infoObject = new GameObject("Info");
        infoObject.transform.SetParent(canvasObject.transform, false);
        runtimeInfoText = infoObject.AddComponent<Text>();
        runtimeInfoText.font = font;
        runtimeInfoText.fontSize = 24;
        runtimeInfoText.alignment = TextAnchor.MiddleCenter;
        runtimeInfoText.color = Color.white;

        RectTransform infoRect = infoObject.GetComponent<RectTransform>();
        infoRect.sizeDelta = new Vector2(950, 80);
        infoRect.anchoredPosition = new Vector2(0, -270);
    }

    private void DrawEmptyChart()
    {
        RawImage targetChart = ActiveChartImage;
        if (targetChart == null)
            return;

        GetChartDimensions(out int renderWidth, out int renderHeight);

        chartTexture = new Texture2D(renderWidth, renderHeight, TextureFormat.RGBA32, false);
        chartTexture.filterMode = FilterMode.Bilinear;

        Color32 background = useTransparentChartBackground
            ? new Color32(0, 0, 0, 0)
            : new Color32(18, 18, 18, 255);
        FillTexture(chartTexture, background);

        Color32 grid = gridColor;
        Color32 axis = axisColor;

        int left = 60;
        int right = 20;
        int top = 20;
        int bottom = 50;
        int plotLeft = left;
        int plotRight = renderWidth - right;
        int plotBottom = bottom;
        int plotTop = renderHeight - top;
        int plotWidth = plotRight - plotLeft;
        int plotHeight = plotTop - plotBottom;

        for (int i = 0; i <= 5; i++)
        {
            int x = plotLeft + Mathf.RoundToInt(plotWidth * (i / 5f));
            DrawLine(chartTexture, x, plotBottom, x, plotTop, grid);
        }

        for (int i = 0; i <= 5; i++)
        {
            int y = plotBottom + Mathf.RoundToInt(plotHeight * (i / 5f));
            DrawLine(chartTexture, plotLeft, y, plotRight, y, grid);
        }

        DrawLine(chartTexture, plotLeft, plotBottom, plotRight, plotBottom, axis);
        DrawLine(chartTexture, plotLeft, plotBottom, plotLeft, plotTop, axis);

        chartTexture.Apply();
        targetChart.texture = chartTexture;
    }

    private void GetChartDimensions(out int width, out int height)
    {
        RawImage targetChart = ActiveChartImage;
        if (manualDesignMode && useChartImageRectSize && targetChart != null)
        {
            Rect rect = targetChart.rectTransform.rect;
            width = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rect.width)), 64, 4096);
            height = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rect.height)), 64, 4096);
            return;
        }

        width = chartWidth;
        height = chartHeight;
    }

    private void FillTexture(Texture2D texture, Color32 color)
    {
        Color32[] pixels = new Color32[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        texture.SetPixels32(pixels);
    }

    private void DrawSmallPoint(Texture2D texture, int centerX, int centerY, Color32 color)
    {
        for (int y = centerY - 1; y <= centerY + 1; y++)
        {
            for (int x = centerX - 1; x <= centerX + 1; x++)
                SetPixelSafe(texture, x, y, color);
        }
    }

    private void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetPixelSafe(texture, x0, y0, color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private void DrawThickLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color, int thickness)
    {
        if (thickness <= 1)
        {
            DrawLine(texture, x0, y0, x1, y1, color);
            return;
        }

        int radius = (thickness - 1) / 2;
        for (int oy = -radius; oy <= radius; oy++)
        {
            for (int ox = -radius; ox <= radius; ox++)
                DrawLine(texture, x0 + ox, y0 + oy, x1 + ox, y1 + oy, color);
        }
    }

    private void SetPixelSafe(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
            return;
        texture.SetPixel(x, y, color);
    }

    private void ApplyChartTitle()
    {
        if (!updateTitleAtRuntime)
            return;

        string title = ActiveChartTitleText;
        if (ActiveTitleTextTmp != null)
            ActiveTitleTextTmp.text = title;
        else if (runtimeTitleText != null)
            runtimeTitleText.text = title;
    }

    private bool IsWatchTimelineActive()
    {
        return lastPacketUnityTime > 0f &&
            Time.realtimeSinceStartup - lastPacketUnityTime < watchTimelineIdleSeconds;
    }

    private string BuildInfoStatus()
    {
        if (IsWatchTimelineActive() &&
            !string.IsNullOrWhiteSpace(currentSessionId) &&
            currentSessionId != FallbackSessionId &&
            sessions.TryGetValue(currentSessionId, out HrSessionData watchSession))
        {
            int count = watchSession.samples.Count;
            if (count >= 2)
            {
                List<HrSample> ordered = watchSession.samples.OrderBy(s => s.measuredAt).ToList();
                float current = ordered[ordered.Count - 1].bpm;
                float avg = ordered.Average(s => s.bpm);
                return string.Format(watchConnectedStatsText, count, current, avg);
            }

            if (count > 0)
                return $"{watchReceivingText} ({count})";

            return watchReceivingText;
        }

        if (usePhysiologyFallbackWhenIdle && physiologyFallback != null)
        {
            float hr = physiologyFallback.CurrentHeartRate;
            if (hr > 0f)
                return string.Format(simulatedHrInfoText, hr);
        }

        return noWatchConnectedText;
    }

    private void RefreshInfoDisplay(bool force = false)
    {
        if (!updateInfoAtRuntime)
            return;

        if (!force && Time.realtimeSinceStartup < nextInfoRefreshTime)
            return;

        nextInfoRefreshTime = Time.realtimeSinceStartup + infoRefreshInterval;

        string text = BuildInfoStatus();
        if (!force && text == lastDisplayedInfo)
            return;

        lastDisplayedInfo = text;
        ApplyInfo(text);
    }

    private void ApplyInfo(string text)
    {
        if (ActiveInfoTextTmp != null)
            ActiveInfoTextTmp.text = text;
        else if (runtimeInfoText != null)
            runtimeInfoText.text = text;
    }

    private void UpdateRuntimeChartTransform(Transform chartTransform = null)
    {
        if (usingDesignerPanel)
            return;

        Transform target = chartTransform != null
            ? chartTransform
            : runtimeCanvas != null ? runtimeCanvas.transform : null;
        if (target == null)
            return;

        if (!createChartInFrontOfCamera)
        {
            target.localPosition = new Vector3(0f, 1.5f, 2f);
            target.localRotation = Quaternion.identity;
            return;
        }

        Camera cam = ResolveViewCamera();
        if (cam == null)
            return;

        if (runtimeCanvas != null)
            runtimeCanvas.worldCamera = cam;

        target.position =
            cam.transform.position +
            cam.transform.forward * distanceFromCamera +
            cam.transform.up * verticalOffset;
        target.rotation = cam.transform.rotation;
    }

    private static Camera ResolveViewCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                return cam;
        }

        return null;
    }

    void OnEnable()
    {
        if (!usingDesignerPanel && runtimeCanvas != null)
            runtimeCanvas.gameObject.SetActive(true);
        else if (usingDesignerPanel)
            ScheduleChartLayoutRefresh();
    }

    void OnDisable()
    {
        if (layoutRefreshRoutine != null)
        {
            StopCoroutine(layoutRefreshRoutine);
            layoutRefreshRoutine = null;
        }

        if (!usingDesignerPanel && runtimeCanvas != null)
            runtimeCanvas.gameObject.SetActive(false);
    }

    private void ScheduleChartLayoutRefresh()
    {
        if (!isActiveAndEnabled || ActiveChartImage == null)
            return;

        if (layoutRefreshRoutine != null)
            StopCoroutine(layoutRefreshRoutine);

        layoutRefreshRoutine = StartCoroutine(RefreshChartAfterLayout());
    }

    private IEnumerator RefreshChartAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        ApplyChartTitle();
        DrawEmptyChart();
        RefreshInfoDisplay(true);
        layoutRefreshRoutine = null;
    }

    void OnDestroy()
    {
        running = false;

        try { udpClient?.Close(); }
        catch { /* Ignore shutdown errors. */ }
    }
}
