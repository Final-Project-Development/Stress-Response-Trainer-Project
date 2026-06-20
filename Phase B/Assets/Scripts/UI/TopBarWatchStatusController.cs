using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-bar watch status — same detection as Baseline calibration, compact label for the toolbar.
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
    [Tooltip("Short toolbar text (Sim · 72 bpm). Full calibration text stays on Baseline_Panel.")]
    public bool useCompactToolbarText = true;
    public float maxToolbarWidth = 180f;

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

    void Awake()
    {
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
        statusText.overflowMode = TextOverflowModes.Ellipsis;
        statusText.raycastTarget = false;

        var rect = statusText.rectTransform;
        if (rect != null && maxToolbarWidth > 0f)
        {
            var size = rect.sizeDelta;
            size.x = maxToolbarWidth;
            rect.sizeDelta = size;
        }
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
            ApplyDisplay("No watch", WorkoutHeartRateChartReceiver.WatchLinkState.Disconnected, force);
            return;
        }

        string label = useCompactToolbarText
            ? workoutChart.GetWatchConnectionStatusTextCompact()
            : workoutChart.GetWatchConnectionStatusText();
        var state = workoutChart.GetWatchLinkState();
        ApplyDisplay(label, state, force);
    }

    void ApplyDisplay(string label, WorkoutHeartRateChartReceiver.WatchLinkState state, bool force)
    {
        if (!force && label == _lastLabel && state == _lastState)
            return;

        _lastLabel = label;
        _lastState = state;

        if (statusText != null)
        {
            statusText.text = label;
            statusText.color = Color.white;
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
}
