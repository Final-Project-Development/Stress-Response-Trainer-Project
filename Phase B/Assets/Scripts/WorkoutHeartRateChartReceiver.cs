using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
    [Header("Network")]
    public int unityListenPort = 5055;

    [Header("Chart")]
    public int chartWidth = 900;
    public int chartHeight = 420;

    [Header("World-space position")]
    public bool createChartInFrontOfCamera = true;
    public float distanceFromCamera = 2.0f;
    public float verticalOffset = -0.15f;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;

    private readonly ConcurrentQueue<string> packetQueue = new ConcurrentQueue<string>();
    private readonly Dictionary<string, HrSessionData> sessions = new Dictionary<string, HrSessionData>();

    private string currentSessionId;
    private float lastPacketUnityTime;
    private bool chartDirty;

    private Canvas canvas;
    private RawImage chartImage;
    private Text titleText;
    private Text infoText;

    private Texture2D chartTexture;

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

        CreateChartUi();
        StartUdpReceiver();

        SetTitle("Workout HR Timeline");
        SetInfo($"Waiting for HR timeline on UDP {unityListenPort}...");
        DrawEmptyChart("Waiting for workout timeline");
    }

    void Update()
    {
        int processed = 0;

        while (processed < 1000 && packetQueue.TryDequeue(out string json))
        {
            processed++;
            HandleJson(json);
        }

        // Safety: if hr_session_end is missed, still draw after packets stop arriving.
        if (chartDirty && Time.realtimeSinceStartup - lastPacketUnityTime > 1.0f)
        {
            RenderCurrentSession();
            chartDirty = false;
        }
    }

    private void StartUdpReceiver()
    {
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
                {
                    packetQueue.Enqueue(json);
                }
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
        {
            return;
        }

        lastPacketUnityTime = Time.realtimeSinceStartup;

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

    private void HandleSessionStart(HrTimelineMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.sessionId))
        {
            return;
        }

        currentSessionId = message.sessionId;

        var session = GetOrCreateSession(message.sessionId);
        session.expectedSampleCount = message.sampleCount;
        session.startedAtRaw = message.startedAt;
        session.endedAtRaw = message.endedAt;

        session.samples.Clear();
        session.sampleKeys.Clear();

        SetTitle("Receiving workout HR timeline...");
        SetInfo($"Session started. Expected samples: {message.sampleCount}");

        chartDirty = true;

        Debug.Log($"HR session started: {message.sessionId}, expected samples: {message.sampleCount}");
    }

    private void HandleHrSample(HrTimelineMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.sessionId))
        {
            return;
        }

        if (message.bpm <= 0f)
        {
            return;
        }

        if (!DateTimeOffset.TryParse(message.measuredAt, out DateTimeOffset measuredAt))
        {
            Debug.LogWarning($"Invalid measuredAt time: {message.measuredAt}");
            return;
        }

        var session = GetOrCreateSession(message.sessionId);

        string key = $"{measuredAt.ToUnixTimeMilliseconds()}|{message.bpm:0.0}";

        if (session.sampleKeys.Contains(key))
        {
            return;
        }

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

        if (session.samples.Count % 25 == 0)
        {
            SetInfo($"Receiving samples: {session.samples.Count}/{message.sampleCount}");
        }
    }

    private void HandleSessionEnd(HrTimelineMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.sessionId))
        {
            currentSessionId = message.sessionId;
        }

        RenderCurrentSession();
        chartDirty = false;

        Debug.Log($"HR session ended: {message.sessionId}");
    }

    private HrSessionData GetOrCreateSession(string sessionId)
    {
        if (!sessions.TryGetValue(sessionId, out HrSessionData session))
        {
            session = new HrSessionData
            {
                sessionId = sessionId
            };

            sessions[sessionId] = session;
        }

        return session;
    }

    private void RenderCurrentSession()
    {
        if (string.IsNullOrWhiteSpace(currentSessionId))
        {
            return;
        }

        if (!sessions.TryGetValue(currentSessionId, out HrSessionData session))
        {
            return;
        }

        RenderSession(session);
    }

    private void RenderSession(HrSessionData session)
    {
        List<HrSample> samples = session.samples
            .OrderBy(s => s.measuredAt)
            .ToList();

        if (samples.Count < 2)
        {
            DrawEmptyChart("Not enough HR samples yet");
            SetInfo($"Samples received: {samples.Count}");
            return;
        }

        DateTimeOffset start = samples.First().measuredAt;
        DateTimeOffset end = samples.Last().measuredAt;

        double durationSeconds = Math.Max(1.0, (end - start).TotalSeconds);

        float minBpm = samples.Min(s => s.bpm);
        float maxBpm = samples.Max(s => s.bpm);
        float avgBpm = samples.Average(s => s.bpm);

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

        chartTexture = new Texture2D(chartWidth, chartHeight, TextureFormat.RGBA32, false);
        chartTexture.filterMode = FilterMode.Point;

        Color32 background = new Color32(18, 18, 18, 255);
        Color32 grid = new Color32(55, 55, 55, 255);
        Color32 axis = new Color32(210, 210, 210, 255);
        Color32 line = new Color32(0, 220, 130, 255);
        Color32 pointColor = new Color32(255, 255, 255, 255);

        FillTexture(chartTexture, background);

        int left = 60;
        int right = 20;
        int top = 20;
        int bottom = 50;

        int plotLeft = left;
        int plotRight = chartWidth - right;
        int plotBottom = bottom;
        int plotTop = chartHeight - top;

        int plotWidth = plotRight - plotLeft;
        int plotHeight = plotTop - plotBottom;

        // Grid.
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

        // Axes.
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
            {
                DrawLine(chartTexture, previous.Value.x, previous.Value.y, current.x, current.y, line);
            }

            DrawSmallPoint(chartTexture, x, y, pointColor);

            previous = current;
        }

        chartTexture.Apply();

        chartImage.texture = chartTexture;

        string localStart = start.ToLocalTime().ToString("HH:mm:ss");
        string localEnd = end.ToLocalTime().ToString("HH:mm:ss");

        TimeSpan duration = end - start;

        SetTitle("Workout HR Timeline");

        SetInfo(
            $"Samples: {samples.Count}/{session.expectedSampleCount} | " +
            $"Time: {localStart} - {localEnd} | " +
            $"Duration: {duration:mm\\:ss} | " +
            $"BPM min/avg/max: {minBpm:0}/{avgBpm:0.0}/{maxBpm:0}"
        );
    }

    private void CreateChartUi()
    {
        GameObject canvasObject = new GameObject("Workout HR Chart Canvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1000, 620);
        canvasObject.transform.localScale = Vector3.one * 0.0018f;

        if (createChartInFrontOfCamera && Camera.main != null)
        {
            Camera cam = Camera.main;

            canvas.worldCamera = cam;

            canvasObject.transform.position =
                cam.transform.position +
                cam.transform.forward * distanceFromCamera +
                cam.transform.up * verticalOffset;

            canvasObject.transform.rotation = cam.transform.rotation;
        }
        else
        {
            canvasObject.transform.localPosition = new Vector3(0f, 1.5f, 2f);
            canvasObject.transform.localRotation = Quaternion.identity;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(canvasObject.transform, false);

        titleText = titleObject.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 34;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(950, 60);
        titleRect.anchoredPosition = new Vector2(0, 270);

        GameObject chartObject = new GameObject("Chart Image");
        chartObject.transform.SetParent(canvasObject.transform, false);

        chartImage = chartObject.AddComponent<RawImage>();

        RectTransform chartRect = chartObject.GetComponent<RectTransform>();
        chartRect.anchorMin = new Vector2(0.5f, 0.5f);
        chartRect.anchorMax = new Vector2(0.5f, 0.5f);
        chartRect.pivot = new Vector2(0.5f, 0.5f);
        chartRect.sizeDelta = new Vector2(chartWidth, chartHeight);
        chartRect.anchoredPosition = new Vector2(0, 15);

        GameObject infoObject = new GameObject("Info");
        infoObject.transform.SetParent(canvasObject.transform, false);

        infoText = infoObject.AddComponent<Text>();
        infoText.font = font;
        infoText.fontSize = 24;
        infoText.alignment = TextAnchor.MiddleCenter;
        infoText.color = Color.white;

        RectTransform infoRect = infoObject.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoRect.pivot = new Vector2(0.5f, 0.5f);
        infoRect.sizeDelta = new Vector2(950, 80);
        infoRect.anchoredPosition = new Vector2(0, -270);
    }

    private void DrawEmptyChart(string message)
    {
        chartTexture = new Texture2D(chartWidth, chartHeight, TextureFormat.RGBA32, false);
        chartTexture.filterMode = FilterMode.Point;

        FillTexture(chartTexture, new Color32(18, 18, 18, 255));

        Color32 axis = new Color32(210, 210, 210, 255);
        DrawLine(chartTexture, 60, 50, chartWidth - 20, 50, axis);
        DrawLine(chartTexture, 60, 50, 60, chartHeight - 20, axis);

        chartTexture.Apply();
        chartImage.texture = chartTexture;

        SetInfo(message);
    }

    private void FillTexture(Texture2D texture, Color32 color)
    {
        Color32[] pixels = new Color32[texture.width * texture.height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels32(pixels);
    }

    private void DrawSmallPoint(Texture2D texture, int centerX, int centerY, Color32 color)
    {
        for (int y = centerY - 1; y <= centerY + 1; y++)
        {
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                SetPixelSafe(texture, x, y, color);
            }
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
            {
                break;
            }

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void SetPixelSafe(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
        {
            return;
        }

        texture.SetPixel(x, y, color);
    }

    private void SetTitle(string text)
    {
        if (titleText != null)
        {
            titleText.text = text;
        }
    }

    private void SetInfo(string text)
    {
        if (infoText != null)
        {
            infoText.text = text;
        }
    }

    void OnDestroy()
    {
        running = false;

        try
        {
            udpClient?.Close();
        }
        catch
        {
            // Ignore shutdown errors.
        }
    }
}