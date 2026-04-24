using System.Collections.Generic;
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
    public Color lineColor = new Color(0.1f, 1f, 0.35f, 1f);
    public int sortingOrder = 200;
    [Tooltip("Extra fallback: draw small UI dots along the graph so data remains visible even if line mesh is clipped.")]
    public bool drawUiDotsFallback = false;
    public float uiDotSize = 5f;

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

            _uiGraphic.SetLinePoints(_uiPointBuffer, lineColor);
            if (drawUiDotsFallback)
                DrawUiDots(_uiPointBuffer);
            else
                ClearUiDots();
        }

        if (_lr == null)
            return;

        if (_useUiOverlayPath)
        {
            _lr.useWorldSpace = false;
            _lr.startWidth = Mathf.Max(2.5f, uiLineWidth);
            _lr.endWidth = Mathf.Max(2.5f, uiLineWidth);
        }

        _lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float u = n == 1 ? 0.5f : i / (float)(n - 1);
            float x = (u - 0.5f) * graphWidth;
            float yNorm = Mathf.Clamp01(values[i] / max);
            float y = useUiRectSpace
                ? (yNorm - 0.5f) * graphHeight
                : yNorm * graphHeight;
            Vector3 p = useWorldSpace
                ? transform.TransformPoint(new Vector3(x, y, 0f))
                : new Vector3(x, y, _useUiOverlayPath ? 0f : uiZOffset);
            _lr.SetPosition(i, p);
        }
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
