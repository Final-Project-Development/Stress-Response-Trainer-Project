using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("Push line slightly toward camera so it is not hidden by panel image.")]
    public float uiZOffset = -1f;
    public Color lineColor = new Color(0.1f, 1f, 0.35f, 1f);
    public int sortingOrder = 200;

    private LineRenderer _lr;
    private StressLineUiGraphic _uiGraphic;
    private bool _useUiOverlayPath;

    private readonly List<Vector2> _uiPointBuffer = new List<Vector2>(256);
    private const float MinRectVisibilityFactor = 0.5f;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _uiGraphic = GetComponent<StressLineUiGraphic>();
        EnsureVisibleUiRectSize();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay && !useWorldSpace)
        {
            if (_uiGraphic == null)
                _uiGraphic = gameObject.AddComponent<StressLineUiGraphic>();
            _useUiOverlayPath = true;
            _lr.enabled = false;
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

        if (_lr.enabled && _lr.material == null)
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
            graphWidth = Mathf.Max(minUiWidth, rt.rect.width);
            graphHeight = Mathf.Max(minUiHeight, rt.rect.height);
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
            if (_lr != null)
                _lr.positionCount = 0;
            return;
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
                : new Vector3(x, y, uiZOffset);
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
}
