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

    private Texture2D chartTexture;
    private Coroutine layoutRefreshRoutine;
    private float sampleIntervalSeconds = 0.4f;
    private readonly List<float> cachedValues = new List<float>(256);
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

    public void Clear()
    {
        hasCachedValues = false;
        cachedValues.Clear();
        DrawEmptyChart();
        chartInfo = string.Empty;
        ApplyInfo(string.Empty);
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
        chartTexture = TextureLineChartRenderer.RenderIndexedSeries(
            cachedValues,
            sampleIntervalSeconds,
            renderWidth,
            renderHeight,
            BuildStyle());

        if (chartImage != null)
            chartImage.texture = chartTexture;

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
            chartImage = CreateUiChild<RawImage>("ChartGraph", new Vector2(0f, -82f), new Vector2(900f, 320f));

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

    private void DrawEmptyChart()
    {
        if (chartImage == null)
            return;

        GetChartDimensions(out int renderWidth, out int renderHeight);
        chartTexture = TextureLineChartRenderer.RenderEmpty(renderWidth, renderHeight, BuildStyle());
        chartImage.texture = chartTexture;
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
        DrawEmptyChart();
        ApplyChartLabels();
        layoutRefreshRoutine = null;
    }
}
