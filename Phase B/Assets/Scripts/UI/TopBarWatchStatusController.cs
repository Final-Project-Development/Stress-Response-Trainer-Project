using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-bar watch status — same detection and wording as Baseline calibration.
/// </summary>
public class TopBarWatchStatusController : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI statusText;
    [Tooltip("Optional colored dot (Image) next to the label.")]
    public Image statusDot;

    [Header("Source (auto-found if empty)")]
    public WorkoutHeartRateChartReceiver workoutChart;

    [Header("Display")]
    [Tooltip("When true, uses the same status strings as Baseline calibration (recommended).")]
    public bool useCalibrationStatusText = true;
    [Tooltip("Optional shorter toolbar text (Sim · 72 bpm).")]
    public bool useCompactToolbarText = false;
    [Tooltip("Max pill width; 0 = no limit.")]
    public float maxToolbarWidth = 320f;
    public float minToolbarWidth = 120f;
    public float toolbarHorizontalPadding = 20f;

    [Header("Refresh")]
    [Tooltip("When empty, uses WorkoutHeartRateChartReceiver.infoRefreshInterval.")]
    public float refreshIntervalSeconds;

    [Header("Colors")]
    public Color connectedColor = new Color(0.35f, 0.9f, 0.55f, 1f);
    public Color receivingColor = new Color(0.45f, 0.82f, 0.98f, 1f);
    public Color simulatedColor = new Color(0.95f, 0.82f, 0.35f, 1f);
    public Color disconnectedColor = new Color(0.75f, 0.78f, 0.82f, 1f);

    float _nextRefreshTime;
    string _lastLabel = string.Empty;
    WorkoutHeartRateChartReceiver.WatchLinkState _lastState =
        WorkoutHeartRateChartReceiver.WatchLinkState.Disconnected;

    TopBarLayoutController _layout;
    RectTransform _containerRect;

    void Awake()
    {
        _containerRect = transform as RectTransform;

        if (statusText == null)
            statusText = GetComponentInChildren<TextMeshProUGUI>(true);

        workoutChart ??= FindFirstObjectByType<WorkoutHeartRateChartReceiver>(FindObjectsInactive.Include);
        _layout = GetComponentInParent<TopBarLayoutController>();
    }

    void OnEnable()
    {
        ConfigureTextRect();
        Refresh(true);
        _layout?.ApplyLayout();
    }

    void Start()
    {
        // Physiology fallback may not be ready on the first OnEnable.
        Refresh(true);
    }

    void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
            return;

        Refresh(false);
    }

    void ConfigureTextRect()
    {
        if (statusText == null)
            return;

        statusText.enableWordWrapping = false;
        statusText.overflowMode = TextOverflowModes.Overflow;
        statusText.raycastTarget = false;
        statusText.margin = Vector4.zero;
        statusText.alignment = TextAlignmentOptions.MidlineLeft;

        var rect = statusText.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 2f);
        rect.offsetMax = new Vector2(-10f, -2f);
    }

    float ResolveRefreshInterval()
    {
        if (refreshIntervalSeconds > 0f)
            return refreshIntervalSeconds;

        return workoutChart != null ? workoutChart.infoRefreshInterval : 0.5f;
    }

    public void Refresh(bool force)
    {
        if (!force && Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, ResolveRefreshInterval());

        if (workoutChart == null)
        {
            ApplyDisplay("No smartwatch connected", WorkoutHeartRateChartReceiver.WatchLinkState.Disconnected, force);
            return;
        }

        string label = ResolveStatusLabel();
        var state = workoutChart.GetWatchLinkState();
        ApplyDisplay(label, state, force);
    }

    string ResolveStatusLabel()
    {
        if (useCompactToolbarText && !useCalibrationStatusText)
            return workoutChart.GetWatchConnectionStatusTextCompact();

        return workoutChart.GetWatchConnectionStatusText();
    }

    void ApplyDisplay(string label, WorkoutHeartRateChartReceiver.WatchLinkState state, bool force)
    {
        if (!force && label == _lastLabel && state == _lastState)
            return;

        _lastLabel = label;
        _lastState = state;

        if (statusText != null)
        {
            statusText.text = label ?? string.Empty;
            statusText.color = Color.white;
            statusText.ForceMeshUpdate();
            ResizeContainerToText();
        }

        if (statusDot != null)
        {
            statusDot.color = state switch
            {
                WorkoutHeartRateChartReceiver.WatchLinkState.Connected => connectedColor,
                WorkoutHeartRateChartReceiver.WatchLinkState.Receiving => receivingColor,
                WorkoutHeartRateChartReceiver.WatchLinkState.Simulated => simulatedColor,
                _ => disconnectedColor
            };
        }

        _layout?.ApplyLayout();
    }

    void ResizeContainerToText()
    {
        if (_containerRect == null || statusText == null)
            return;

        float preferred = statusText.preferredWidth + toolbarHorizontalPadding;
        if (maxToolbarWidth > 0f)
            preferred = Mathf.Min(preferred, maxToolbarWidth);

        preferred = Mathf.Max(preferred, minToolbarWidth);

        var size = _containerRect.sizeDelta;
        size.x = preferred;
        _containerRect.sizeDelta = size;
    }
}
