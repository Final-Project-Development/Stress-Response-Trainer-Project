using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Polyline for SCI, HRV, or other series over time.
/// On <b>Screen Space Overlay</b> canvases, uses <see cref="StressLineUiGraphic"/> because <see cref="LineRenderer"/> draws behind the UI and is invisible.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class SimpleStressLineGraph : MonoBehaviour
{
    public float widthMeters = 0.8f;
    public float heightMeters = 0.35f;
    public float maxSciDisplay = 80f;
    [Tooltip("Disable for UI/Canvas usage so points are drawn in local space.")]
    public bool useWorldSpace = false;
    [Tooltip("When in UI mode, use RectTransform size (pixels) as graph size.")]
    public bool useRectTransformSizeForUi = true;
    [Tooltip("Line width used in UI mode (pixels-ish local units).")]
    public float uiLineWidth = 3f;
    public float minUiWidth = 500f;
    public float minUiHeight = 180f;
    [Tooltip("Clamp UI graph width so stretched RectTransforms do not spread points across full screen.")]
    public float maxUiWidth = 760f;
    [Tooltip("Clamp UI graph height for stable in-panel rendering.")]
    public float maxUiHeight = 220f;
    [Tooltip("Push line slightly toward camera so it is not hidden by panel image.")]
    public float uiZOffset = -1f;
    [Tooltip("Main trace color (line + key for area tint).")]
    public Color lineColor = new Color(0.3f, 0.75f, 0.95f, 1f);
    public int sortingOrder = 200;
    [Tooltip("Extra fallback: draw small UI dots along the graph so data remains visible even if line mesh is clipped.")]
    public bool drawUiDotsFallback = false;
    public float uiDotSize = 5f;

    [Header("Chart look (UI canvas)")]
    [Tooltip("Fills the plot area, grid, and frame. World-space LineRenderer is unchanged.")]
    public bool useProfessionalChartStyle = true;
    [Tooltip("Thin border around the data area so the graph reads as a chart, not a random line.")]
    public bool showPlotFrame = true;
    [Tooltip("Horizontal reference lines to read magnitude at a glance.")]
    public bool showHorizontalGrid = true;
    [Range(2, 12)]
    public int horizontalGridLines = 5;
    [Tooltip("Shaded region under the curve to emphasize the stress trace.")]
    public bool showAreaUnderCurve = true;
    public Color frameAndGridColor = new Color(0.42f, 0.5f, 0.58f, 0.5f);
    [Tooltip("If alpha is ~0, the area uses the line color with low opacity.")]
    public Color areaTopColor = new Color(0f, 0f, 0f, 0f);
    public Color areaBottomColor = new Color(0.02f, 0.04f, 0.08f, 0.75f);
    [Tooltip("Optional title, e.g. 'SCI (%)' or 'HRV (ms)'. Fills chartTitleText when set.")]
    public string chartTitle = "";
    [Tooltip("Optional: assign a TMP above the graph — title text is set at runtime if chartTitle is non-empty.")]
    public TextMeshProUGUI chartTitleText;
    [Tooltip("Optional: Y axis max (top) — e.g. scale maximum.")]
    public TextMeshProUGUI yAxisLabelTop;
    [Tooltip("Optional: Y axis min (bottom), usually 0 for normalized plots.")]
    public TextMeshProUGUI yAxisLabelBottom;
    public TextMeshProUGUI xAxisLabelStart;
    public TextMeshProUGUI xAxisLabelEnd;
    [Tooltip("Shown on the left of the time axis (session start).")]
    public string xLabelStart = "Start";
    [Tooltip("Shown on the right of the time axis (session end).")]
    public string xLabelEnd = "End";
    [Tooltip("Y axis min label, typically 0 for SCI/HRV in this layout.")]
    public string yLabelBottomText = "0";

    private LineRenderer _lr;
    private StressLineUiGraphic _uiGraphic;
    private bool _useUiOverlayPath;

    private readonly List<Vector2> _uiPointBuffer = new List<Vector2>(256);
    private readonly List<Image> _uiDotPool = new List<Image>(256);
    private const float MinRectVisibilityFactor = 0.5f;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _uiGraphic = GetComponent<StressLineUiGraphic>();
        EnsureVisibleUiRectSize();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && !useWorldSpace)
        {
            if (_uiGraphic == null)
                _uiGraphic = gameObject.AddComponent<StressLineUiGraphic>();
            _useUiOverlayPath = true;
            _lr.enabled = true;
        }
        else
        {
            _useUiOverlayPath = false;
            _lr.enabled = true;
        }

        _lr.useWorldSpace = useWorldSpace;
        _lr.positionCount = 0;
        if (useWorldSpace)
        {
            _lr.startWidth = 0.006f;
            _lr.endWidth = 0.006f;
        }
        else if (!_useUiOverlayPath)
        {
            _lr.startWidth = uiLineWidth;
            _lr.endWidth = uiLineWidth;
        }

        if (_lr.material == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
                _lr.material = new Material(shader);
        }

        _lr.startColor = lineColor;
        _lr.endColor = lineColor;
        _lr.textureMode = LineTextureMode.Stretch;
        _lr.numCapVertices = 6;
        _lr.numCornerVertices = 4;
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows = false;
        _lr.sortingOrder = sortingOrder;

        if (_uiGraphic != null)
        {
            _uiGraphic.raycastTarget = false;
            _uiGraphic.SetThicknessPixels(uiLineWidth);
        }

        // Do not add a nested Canvas here — it breaks batching / materials (pink "error" shader in Scene view).
        // Z-order: TrainingFlowController moves graph transforms before buttons after layout.
    }

    public void SetFromSciPoints(IReadOnlyList<float> sciPoints)
    {
        SetFromValues(sciPoints, maxSciDisplay);
    }

    /// <summary>Clears the line (e.g. when no samples for this metric).</summary>
    public void Clear()
    {
        if (_lr == null)
            _lr = GetComponent<LineRenderer>();
        if (_lr != null)
            _lr.positionCount = 0;

        if (_uiGraphic == null)
            _uiGraphic = GetComponent<StressLineUiGraphic>();
        _uiGraphic?.ClearChartOptions();
        _uiGraphic?.ClearLine();
        ClearUiDots();
    }

    public void SetFromValues(IReadOnlyList<float> values, float maxDisplayValue)
    {
        EnsureVisibleUiRectSize();

        if (values == null || values.Count == 0)
        {
            Clear();
            return;
        }

        if (_uiGraphic == null)
            _uiGraphic = GetComponent<StressLineUiGraphic>();

        int n = values.Count;
        float max = Mathf.Max(1f, maxDisplayValue);
        for (int i = 0; i < n; i++)
            max = Mathf.Max(max, values[i] + 5f);

        float graphWidth = widthMeters;
        float graphHeight = heightMeters;
        bool useUiRectSpace = !useWorldSpace && useRectTransformSizeForUi;
        if (useUiRectSpace && TryGetComponent<RectTransform>(out var rt))
        {
            float clampedWidth = Mathf.Clamp(rt.rect.width, Mathf.Max(50f, minUiWidth), Mathf.Max(minUiWidth, maxUiWidth));
            float clampedHeight = Mathf.Clamp(rt.rect.height, Mathf.Max(40f, minUiHeight), Mathf.Max(minUiHeight, maxUiHeight));
            graphWidth = clampedWidth;
            graphHeight = clampedHeight;
            if (_lr != null && _lr.enabled)
            {
                _lr.startWidth = uiLineWidth;
                _lr.endWidth = uiLineWidth;
            }
        }

        if (_useUiOverlayPath && _uiGraphic != null)
        {
            _uiPointBuffer.Clear();
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0.5f : i / (float)(n - 1);
                float x = (u - 0.5f) * graphWidth;
                float yNorm = Mathf.Clamp01(values[i] / max);
                float y = (yNorm - 0.5f) * graphHeight;
                _uiPointBuffer.Add(new Vector2(x, y));
            }

            if (useProfessionalChartStyle)
            {
                Color at = areaTopColor;
                if (at.a < 0.01f)
                    at = new Color(lineColor.r, lineColor.g, lineColor.b, 0.38f);
                _uiGraphic.SetChartOptions(
                    graphWidth,
                    graphHeight,
                    showPlotFrame,
                    showHorizontalGrid,
                    horizontalGridLines,
                    showAreaUnderCurve,
                    frameAndGridColor,
                    at,
                    areaBottomColor);
            }
            else
                _uiGraphic.ClearChartOptions();

            _uiGraphic.SetLinePoints(_uiPointBuffer, lineColor);
            if (drawUiDotsFallback)
                DrawUiDots(_uiPointBuffer);
            else
                ClearUiDots();

            ApplyRuntimeChartLabels(max);
        }

        if (_lr == null)
            return;

        if (_useUiOverlayPath)
        {
            _lr.useWorldSpace = false;
            _lr.startWidth = Mathf.Max(2.5f, uiLineWidth);
            _lr.endWidth = Mathf.Max(2.5f, uiLineWidth);
        }

        int lrCount = n == 1 ? 2 : n;
        _lr.positionCount = lrCount;
        float yOne = 0f;
        if (n == 1)
        {
            float yNorm0 = Mathf.Clamp01(values[0] / max);
            yOne = useUiRectSpace
                ? (yNorm0 - 0.5f) * graphHeight
                : yNorm0 * graphHeight;
        }

        for (int i = 0; i < lrCount; i++)
        {
            int valueIndex = n == 1 ? 0 : i;
            float u;
            if (n == 1)
                u = i == 0 ? 0.5f - 0.0005f : 0.5f + 0.0005f; // two distinct samples so the line can render
            else
                u = i / (float)(n - 1);
            float x = (u - 0.5f) * graphWidth;

            float yNorm = Mathf.Clamp01(values[valueIndex] / max);
            float y = n == 1
                ? yOne
                : (useUiRectSpace
                ? (yNorm - 0.5f) * graphHeight
                : yNorm * graphHeight);
            Vector3 p = useWorldSpace
                ? transform.TransformPoint(new Vector3(x, y, 0f))
                : new Vector3(x, y, _useUiOverlayPath ? 0f : uiZOffset);
            _lr.SetPosition(i, p);
        }
    }

    private void ApplyRuntimeChartLabels(float axisMax)
    {
        if (!string.IsNullOrEmpty(chartTitle) && chartTitleText != null)
            chartTitleText.text = chartTitle;
        if (yAxisLabelTop != null)
            yAxisLabelTop.text = axisMax < 1f ? axisMax.ToString("F1") : axisMax.ToString("F0");
        if (yAxisLabelBottom != null)
            yAxisLabelBottom.text = yLabelBottomText;
        if (xAxisLabelStart != null)
            xAxisLabelStart.text = xLabelStart;
        if (xAxisLabelEnd != null)
            xAxisLabelEnd.text = xLabelEnd;
    }

    /// <summary>
    /// Some scene setups accidentally leave graph RectTransforms at near-zero width (e.g. 0.01),
    /// which causes UI clipping and makes lines look "missing". Enforce a practical minimum size.
    /// </summary>
    private void EnsureVisibleUiRectSize()
    {
        if (useWorldSpace || !useRectTransformSizeForUi)
            return;

        if (!TryGetComponent<RectTransform>(out var rt))
            return;

        float minWidth = Mathf.Max(1f, minUiWidth);
        float minHeight = Mathf.Max(1f, minUiHeight);
        float currentWidth = Mathf.Abs(rt.rect.width);
        float currentHeight = Mathf.Abs(rt.rect.height);

        if (currentWidth < minWidth * MinRectVisibilityFactor)
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, minWidth);

        if (currentHeight < minHeight * MinRectVisibilityFactor)
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minHeight);
    }

    private void DrawUiDots(IReadOnlyList<Vector2> points)
    {
        if (!TryGetComponent<RectTransform>(out _))
            return;

        int needed = points != null ? points.Count : 0;
        EnsureDotPoolSize(needed);

        for (int i = 0; i < _uiDotPool.Count; i++)
        {
            bool on = i < needed;
            var dot = _uiDotPool[i];
            if (dot == null)
                continue;

            dot.gameObject.SetActive(on);
            if (!on)
                continue;

            dot.color = lineColor;
            var rt = dot.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = points[i];
            float s = Mathf.Max(2f, uiDotSize);
            rt.sizeDelta = new Vector2(s, s);
        }
    }

    private void EnsureDotPoolSize(int needed)
    {
        while (_uiDotPool.Count < needed)
        {
            var go = new GameObject($"GraphDot_{_uiDotPool.Count}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = GetUiWhiteSprite();
            _uiDotPool.Add(img);
        }
    }

    private void ClearUiDots()
    {
        for (int i = 0; i < _uiDotPool.Count; i++)
        {
            if (_uiDotPool[i] != null)
                _uiDotPool[i].gameObject.SetActive(false);
        }
    }

    private static Sprite _uiWhiteSprite;
    private static Sprite GetUiWhiteSprite()
    {
        if (_uiWhiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            _uiWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        return _uiWhiteSprite;
    }
}
