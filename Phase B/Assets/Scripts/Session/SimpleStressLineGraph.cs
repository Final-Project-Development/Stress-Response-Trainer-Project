using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Results-panel line chart using the same texture renderer as the live/baseline HR chart.
/// </summary>
public class SimpleStressLineGraph : MonoBehaviour
{
    [Header("Designer UI")]
    [Tooltip("RawImage where the chart texture is drawn (child ChartGraph recommended).")]
    public RawImage chartImage;
    [Tooltip("Optional ChartTitle TMP.")]
    public TextMeshProUGUI chartTitleText;
    [Tooltip("Optional ChartInfoText TMP.")]
    public TextMeshProUGUI chartInfoText;
    public string chartTitle = "";
    [TextArea] public string chartInfo = "";

    [Header("Data (legacy inspector fields)")]
    public float maxSciDisplay = 80f;

    [Header("Manual design")]
    public bool manualDesignMode = true;
    public bool useChartImageRectSize = true;
    public bool updateTitleAtRuntime = true;
    public bool updateInfoAtRuntime = true;

    [Header("Chart render")]
    public int chartWidth = 900;
    public int chartHeight = 420;
    public bool useTransparentChartBackground = true;
    public Color lineColor = new Color(0.3f, 0.75f, 0.95f, 1f);
    public int chartLineWidth = 3;
    public Color gridColor = new Color(0.42f, 0.5f, 0.58f, 0.45f);
    public Color axisColor = new Color(0.75f, 0.82f, 0.9f, 0.9f);
    public Color pointColor = Color.white;
    [Tooltip("When enabled, chartImage/caption rects are not moved at runtime.")]
    public bool preserveManualLayout = false;

    [Header("Axis value labels")]
    public bool showAxisValueLabels = true;
    public Color axisLabelColor = new Color(0.85f, 0.9f, 1f, 0.92f);
    public float axisLabelFontSize = 13f;
    public string yAxisValueSuffix = "%";
    [Tooltip("Use a fixed Y range (recommended for SCI % charts).")]
    public bool useFixedYRange = false;
    public float fixedYMin = 0f;
    public float fixedYMax = 0f;
    public AxisXLabelMode xAxisLabelMode = AxisXLabelMode.RunIndex;

    public enum AxisXLabelMode
    {
        RunIndex,
        TimeSeconds
    }

    private Texture2D chartTexture;
    private Coroutine layoutRefreshRoutine;
    private float sampleIntervalSeconds = 0.4f;
    private readonly List<float> cachedValues = new List<float>(256);
    private readonly List<TextMeshProUGUI> yAxisLabels = new List<TextMeshProUGUI>(6);
    private readonly List<TextMeshProUGUI> xAxisLabels = new List<TextMeshProUGUI>(6);
    private RectTransform axisLabelsRoot;
    private bool hasCachedValues;

    void Awake()
    {
        DisableLegacyRenderers();
        ResolveDesignerReferences();
        EnsureDesignerChartUi();
    }

    void OnEnable()
    {
        if (hasCachedValues)
            RenderCachedValues();
        else
            ScheduleChartLayoutRefresh();
    }

    void OnDisable()
    {
        if (layoutRefreshRoutine != null)
        {
            StopCoroutine(layoutRefreshRoutine);
            layoutRefreshRoutine = null;
        }
    }

    public void SetFromSciPoints(IReadOnlyList<float> sciPoints, float intervalSeconds = 0.4f)
    {
        SetFromValues(sciPoints, maxSciDisplay, intervalSeconds);
    }

    public void SetFromValues(IReadOnlyList<float> values, float maxDisplayValue, float intervalSeconds = 0.4f)
    {
        sampleIntervalSeconds = Mathf.Max(0.001f, intervalSeconds);
        ResolveDesignerReferences();

        if (values == null || values.Count == 0)
        {
            Clear();
            return;
        }

        cachedValues.Clear();
        for (int i = 0; i < values.Count; i++)
            cachedValues.Add(values[i]);
        hasCachedValues = true;

        if (values.Count == 1)
        {
            DrawEmptyChart();
            ApplyChartLabels();
            return;
        }

        RenderCachedValues();
    }

    public void SetInfoText(string text)
    {
        chartInfo = text ?? string.Empty;
        ApplyInfo(chartInfo);
    }

    /// <summary>Re-draw using the current ChartGraph RectTransform size (manual layout friendly).</summary>
    public void RefreshRenderToFitLayout()
    {
        ResolveDesignerReferences();
        if (hasCachedValues)
            RenderCachedValues();
        else
            DrawEmptyChart();
    }

    /// <summary>Fills the host RectTransform area with caption strip + chart inset.</summary>
    public void ApplyInsetFillLayout(float left, float bottom, float right, float top, float captionHeight = 28f)
    {
        if (preserveManualLayout)
            return;

        ResolveDesignerReferences();
        EnsureDesignerChartUi();

        if (chartTitleText != null)
            chartTitleText.gameObject.SetActive(false);

        if (chartInfoText != null && captionHeight > 0f)
        {
            var caption = chartInfoText.rectTransform;
            caption.gameObject.SetActive(true);
            caption.anchorMin = new Vector2(0f, 1f);
            caption.anchorMax = new Vector2(1f, 1f);
            caption.pivot = new Vector2(0.5f, 1f);
            caption.anchoredPosition = Vector2.zero;
            caption.offsetMin = new Vector2(left, -captionHeight);
            caption.offsetMax = new Vector2(-right, 0f);
        }

        if (chartImage != null)
        {
            var graph = chartImage.rectTransform;
            graph.anchorMin = Vector2.zero;
            graph.anchorMax = Vector2.one;
            graph.pivot = new Vector2(0.5f, 0.5f);
            graph.anchoredPosition = Vector2.zero;
            graph.offsetMin = new Vector2(left, bottom);
            float topInset = top + (captionHeight > 0f ? captionHeight : 0f);
            graph.offsetMax = new Vector2(-right, -topInset);
        }
    }

    public void Clear()
    {
        hasCachedValues = false;
        cachedValues.Clear();
        DrawEmptyChart();
        chartInfo = string.Empty;
        ApplyInfo(string.Empty);
        HideAxisValueLabels();
    }

    private void RenderCachedValues()
    {
        if (!hasCachedValues || cachedValues.Count == 0)
        {
            DrawEmptyChart();
            ApplyChartLabels();
            return;
        }

        if (cachedValues.Count == 1)
        {
            DrawEmptyChart();
            ApplyChartLabels();
            return;
        }

        GetChartDimensions(out int renderWidth, out int renderHeight);
        var style = BuildStyle();
        ComputeYRangeForDisplay(out float yMin, out float yMax);

        var points = new List<TextureLineChartRenderer.TimeValuePoint>(cachedValues.Count);
        for (int i = 0; i < cachedValues.Count; i++)
            points.Add(new TextureLineChartRenderer.TimeValuePoint(i * sampleIntervalSeconds, cachedValues[i]));

        double duration = Mathf.Max(1f, (cachedValues.Count - 1) * sampleIntervalSeconds);
        chartTexture = TextureLineChartRenderer.RenderTimeSeries(
            points,
            duration,
            renderWidth,
            renderHeight,
            style,
            yMin,
            yMax);

        if (chartImage != null)
            chartImage.texture = chartTexture;

        UpdateAxisValueLabels(yMin, yMax, cachedValues.Count, duration);
        ApplyChartLabels();
    }

    private void DisableLegacyRenderers()
    {
        var lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null)
            lineRenderer.enabled = false;

        var uiGraphic = GetComponent<StressLineUiGraphic>();
        if (uiGraphic != null)
            uiGraphic.enabled = false;
    }

    private void ResolveDesignerReferences()
    {
        if (chartImage == null)
            chartImage = transform.Find("ChartGraph")?.GetComponent<RawImage>();

        if (chartTitleText == null)
            chartTitleText = transform.Find("ChartTitle")?.GetComponent<TextMeshProUGUI>();

        if (chartInfoText == null)
            chartInfoText = transform.Find("ChartInfoText")?.GetComponent<TextMeshProUGUI>();
    }

    private void EnsureDesignerChartUi()
    {
        if (chartImage == null)
        {
            if (preserveManualLayout)
                chartImage = CreateStretchFillUiChild<RawImage>("ChartGraph");
            else
                chartImage = CreateUiChild<RawImage>("ChartGraph", new Vector2(0f, -82f), new Vector2(900f, 320f));
        }

        if (preserveManualLayout && chartImage != null)
        {
            if (chartTitleText != null)
                chartTitleText.gameObject.SetActive(false);
            return;
        }

        if (chartTitleText == null)
            chartTitleText = CreateUiChild<TextMeshProUGUI>("ChartTitle", new Vector2(-185f, 159f), new Vector2(500f, 100f));

        if (chartInfoText == null)
            chartInfoText = CreateUiChild<TextMeshProUGUI>("ChartInfoText", new Vector2(-209f, 91f), new Vector2(700f, 50f));

        if (chartTitleText != null)
        {
            chartTitleText.alignment = TextAlignmentOptions.Left;
            chartTitleText.fontSize = 40f;
        }

        if (chartInfoText != null)
        {
            chartInfoText.alignment = TextAlignmentOptions.Left;
            chartInfoText.fontSize = 28f;
        }
    }

    private T CreateUiChild<T>(string childName, Vector2 anchoredPosition, Vector2 sizeDelta) where T : Component
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
            return existing.GetComponent<T>();

        var go = new GameObject(childName, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        if (typeof(T) == typeof(RawImage))
            return go.AddComponent<RawImage>() as T;

        if (typeof(T) == typeof(TextMeshProUGUI))
            return go.AddComponent<TextMeshProUGUI>() as T;

        return go.AddComponent<T>();
    }

    private T CreateStretchFillUiChild<T>(string childName) where T : Component
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
            return existing.GetComponent<T>();

        var go = new GameObject(childName, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (typeof(T) == typeof(RawImage))
            return go.AddComponent<RawImage>() as T;

        if (typeof(T) == typeof(TextMeshProUGUI))
            return go.AddComponent<TextMeshProUGUI>() as T;

        return go.AddComponent<T>();
    }

    private void DrawEmptyChart()
    {
        if (chartImage == null)
            return;

        GetChartDimensions(out int renderWidth, out int renderHeight);
        chartTexture = TextureLineChartRenderer.RenderEmpty(renderWidth, renderHeight, BuildStyle());
        chartImage.texture = chartTexture;

        if (useFixedYRange)
        {
            float yMax = fixedYMax > 0f ? fixedYMax : maxSciDisplay;
            UpdateAxisValueLabels(fixedYMin, yMax, 0, 1.0);
        }
        else
            HideAxisValueLabels();
    }

    private TextureLineChartRenderer.Style BuildStyle()
    {
        var style = TextureLineChartRenderer.Style.Default;
        style.useTransparentBackground = useTransparentChartBackground;
        style.lineColor = lineColor;
        style.chartLineWidth = chartLineWidth;
        style.gridColor = gridColor;
        style.axisColor = axisColor;
        style.pointColor = pointColor;
        return style;
    }

    private void GetChartDimensions(out int width, out int height)
    {
        if (manualDesignMode && useChartImageRectSize && chartImage != null)
        {
            Rect rect = chartImage.rectTransform.rect;
            width = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rect.width)), 64, 4096);
            height = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(rect.height)), 64, 4096);
            return;
        }

        width = chartWidth;
        height = chartHeight;
    }

    private void ApplyChartLabels()
    {
        if (updateTitleAtRuntime && chartTitleText != null && !string.IsNullOrEmpty(chartTitle))
            chartTitleText.text = chartTitle;

        if (updateInfoAtRuntime)
            ApplyInfo(chartInfo);
    }

    private void ApplyInfo(string text)
    {
        if (chartInfoText != null)
            chartInfoText.text = text;
    }

    private void ComputeYRangeForDisplay(out float yMin, out float yMax)
    {
        if (useFixedYRange)
        {
            yMin = fixedYMin;
            yMax = fixedYMax > 0f ? fixedYMax : maxSciDisplay;
            return;
        }

        float minValue = cachedValues[0];
        float maxValue = cachedValues[0];
        for (int i = 1; i < cachedValues.Count; i++)
        {
            minValue = Mathf.Min(minValue, cachedValues[i]);
            maxValue = Mathf.Max(maxValue, cachedValues[i]);
        }

        TextureLineChartRenderer.ComputeYRange(minValue, maxValue, out yMin, out yMax);
    }

    private void EnsureAxisValueLabels()
    {
        if (chartImage == null)
            return;

        if (axisLabelsRoot == null)
        {
            var rootGo = new GameObject("ChartAxisLabels", typeof(RectTransform));
            axisLabelsRoot = rootGo.GetComponent<RectTransform>();
            axisLabelsRoot.SetParent(chartImage.transform, false);
            axisLabelsRoot.anchorMin = Vector2.zero;
            axisLabelsRoot.anchorMax = Vector2.one;
            axisLabelsRoot.offsetMin = Vector2.zero;
            axisLabelsRoot.offsetMax = Vector2.zero;
        }

        const int tickCount = 5;
        EnsureAxisLabelSet(yAxisLabels, "Y", tickCount + 1, TextAlignmentOptions.MidlineRight);
        EnsureAxisLabelSet(xAxisLabels, "X", tickCount + 1, TextAlignmentOptions.Top);
    }

    private void EnsureAxisLabelSet(
        List<TextMeshProUGUI> labels,
        string prefix,
        int count,
        TextAlignmentOptions alignment)
    {
        TMP_FontAsset font = chartInfoText != null
            ? chartInfoText.font
            : chartTitleText != null
                ? chartTitleText.font
                : TMP_Settings.defaultFontAsset;

        while (labels.Count < count)
        {
            int index = labels.Count;
            var go = new GameObject($"{prefix}AxisLabel_{index}", typeof(RectTransform));
            go.transform.SetParent(axisLabelsRoot, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.font = font;
            label.fontSize = axisLabelFontSize;
            label.color = axisLabelColor;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = axisLabelFontSize;
            label.alignment = alignment;
            label.overflowMode = TextOverflowModes.Overflow;

            var rt = label.rectTransform;
            rt.sizeDelta = prefix == "Y" ? new Vector2(52f, 22f) : new Vector2(36f, 22f);
            labels.Add(label);
        }
    }

    private void UpdateAxisValueLabels(float yMin, float yMax, int pointCount, double durationSeconds)
    {
        if (!showAxisValueLabels || chartImage == null)
        {
            HideAxisValueLabels();
            return;
        }

        EnsureAxisValueLabels();

        var style = BuildStyle();
        Rect chartRect = chartImage.rectTransform.rect;
        float plotLeft = style.plotLeft;
        float plotBottom = style.plotBottom;
        float plotWidth = chartRect.width - style.plotLeft - style.plotRight;
        float plotHeight = chartRect.height - style.plotBottom - style.plotTop;

        const int tickCount = 5;
        float scaledFont = Mathf.Clamp(axisLabelFontSize, 9f, Mathf.Max(9f, chartRect.height * 0.045f));

        for (int i = 0; i <= tickCount; i++)
        {
            float t = i / (float)tickCount;
            float yValue = Mathf.Lerp(yMin, yMax, t);
            float yPos = plotBottom + plotHeight * t;

            TextMeshProUGUI label = yAxisLabels[i];
            label.fontSize = scaledFont;
            label.fontSizeMax = scaledFont;
            label.color = axisLabelColor;
            label.text = FormatYAxisValue(yValue);
            label.gameObject.SetActive(true);
            PlaceAxisLabel(label, plotLeft - 6f, yPos, new Vector2(1f, 0.5f));
        }

        for (int i = 0; i <= tickCount; i++)
        {
            float t = i / (float)tickCount;
            float xPos = plotLeft + plotWidth * t;

            TextMeshProUGUI label = xAxisLabels[i];
            label.fontSize = scaledFont;
            label.fontSizeMax = scaledFont;
            label.color = axisLabelColor;
            label.text = FormatXAxisValue(i, tickCount, pointCount, durationSeconds);
            label.gameObject.SetActive(pointCount >= 2 && !string.IsNullOrEmpty(label.text));
            PlaceAxisLabel(label, xPos, plotBottom - 8f, new Vector2(0.5f, 1f));
        }

        axisLabelsRoot.SetAsLastSibling();
    }

    private void PlaceAxisLabel(TextMeshProUGUI label, float x, float y, Vector2 pivot)
    {
        RectTransform rt = label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = pivot;
        rt.anchoredPosition = new Vector2(x, y);
    }

    private string FormatYAxisValue(float value)
    {
        string numeric = Mathf.Abs(value - Mathf.Round(value)) < 0.05f
            ? Mathf.RoundToInt(value).ToString()
            : value.ToString("0.0");
        return string.IsNullOrEmpty(yAxisValueSuffix) ? numeric : numeric + yAxisValueSuffix;
    }

    private string FormatXAxisValue(int tickIndex, int tickCount, int pointCount, double durationSeconds)
    {
        if (pointCount < 2)
            return string.Empty;

        float t = tickIndex / (float)tickCount;

        switch (xAxisLabelMode)
        {
            case AxisXLabelMode.TimeSeconds:
                return Mathf.RoundToInt((float)(durationSeconds * t)).ToString();

            case AxisXLabelMode.RunIndex:
            default:
                int runNumber = 1 + Mathf.RoundToInt((pointCount - 1) * t);
                return runNumber.ToString();
        }
    }

    private void HideAxisValueLabels()
    {
        for (int i = 0; i < yAxisLabels.Count; i++)
            yAxisLabels[i].gameObject.SetActive(false);
        for (int i = 0; i < xAxisLabels.Count; i++)
            xAxisLabels[i].gameObject.SetActive(false);
    }

    private void ScheduleChartLayoutRefresh()
    {
        if (!isActiveAndEnabled || chartImage == null)
            return;

        if (layoutRefreshRoutine != null)
            StopCoroutine(layoutRefreshRoutine);

        layoutRefreshRoutine = StartCoroutine(RefreshChartAfterLayout());
    }

    private IEnumerator RefreshChartAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (hasCachedValues)
            RenderCachedValues();
        else
            DrawEmptyChart();
        ApplyChartLabels();
        layoutRefreshRoutine = null;
    }
}
