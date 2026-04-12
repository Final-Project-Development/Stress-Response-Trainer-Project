using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World-space polyline for SCI or stress over time. Attach to a child of a world canvas or empty in front of the user.
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

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.useWorldSpace = useWorldSpace;
        _lr.positionCount = 0;
        if (useWorldSpace)
        {
            _lr.startWidth = 0.006f;
            _lr.endWidth = 0.006f;
        }
        else
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
    }

    public void SetFromSciPoints(IReadOnlyList<float> sciPoints)
    {
        SetFromValues(sciPoints, maxSciDisplay);
    }

    public void SetFromValues(IReadOnlyList<float> values, float maxDisplayValue)
    {
        if (values == null || values.Count == 0)
        {
            _lr.positionCount = 0;
            return;
        }

        int n = values.Count;
        _lr.positionCount = n;
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
            _lr.startWidth = uiLineWidth;
            _lr.endWidth = uiLineWidth;
        }

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
}
