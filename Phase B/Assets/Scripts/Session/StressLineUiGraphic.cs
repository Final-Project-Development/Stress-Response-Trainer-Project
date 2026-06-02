using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a polyline on Canvas. Uses <see cref="Graphic"/> (not <see cref="MaskableGraphic"/>) so parent <see cref="RectMask2D"/> won't clip the mesh incorrectly.
/// Adds a nested <see cref="Canvas"/> with override sorting so the line draws above sibling UI (e.g. results text columns).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class StressLineUiGraphic : Graphic
{
    [SerializeField] private float lineThicknessPixels = 3f;
    private readonly List<Vector2> _points = new List<Vector2>(256);
    private Color _lineColor = Color.white;

    private bool _useChart;
    private float _plotW;
    private float _plotH;
    private bool _showFrame;
    private bool _showGrid;
    private int _gridLinesH = 5;
    private Color _gridColor;
    private bool _showArea;
    private Color _areaColorTop;
    private Color _areaColorBottom;

    public void SetThicknessPixels(float px)
    {
        lineThicknessPixels = Mathf.Max(0.5f, px);
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        // Vertex colors are multiplied by Graphic.color in the UI shader — keep tint neutral.
        color = Color.white;
        // Avoid missing-shader pink materials in some SRP / nested-canvas cases.
        if (material == null || material.shader == null || material.shader.name.Contains("Hidden/InternalErrorShader"))
        {
            var def = Canvas.GetDefaultCanvasMaterial();
            if (def != null)
                material = def;
        }
    }

    public void ClearLine()
    {
        _points.Clear();
        _useChart = false;
        SetAllDirty();
    }

    /// <summary>
    /// When enabled, also draws a frame, optional horizontal grid, and area under the curve in plot coordinates.
    /// Plot bounds: x in [-w/2, w/2], y in [-h/2, h/2] (must match <see cref="SetLinePoints"/>).
    /// </summary>
    public void SetChartOptions(
        float plotW,
        float plotH,
        bool showFrame,
        bool showGrid,
        int horizontalGridCount,
        bool showArea,
        Color gridColor,
        Color areaColorTop,
        Color areaColorBottom)
    {
        _plotW = Mathf.Max(1f, plotW);
        _plotH = Mathf.Max(1f, plotH);
        _showFrame = showFrame;
        _showGrid = showGrid;
        _gridLinesH = Mathf.Clamp(horizontalGridCount, 2, 24);
        _showArea = showArea;
        _gridColor = gridColor;
        _areaColorTop = areaColorTop;
        _areaColorBottom = areaColorBottom;
        _useChart = _showFrame || _showGrid || _showArea;
        SetAllDirty();
    }

    public void ClearChartOptions()
    {
        _useChart = false;
        SetAllDirty();
    }

    /// <summary>Points in local space of this RectTransform (same convention as SimpleStressLineGraph).</summary>
    public void SetLinePoints(IReadOnlyList<Vector2> points, Color lineColor)
    {
        _points.Clear();
        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
                _points.Add(points[i]);
        }

        _lineColor = lineColor;
        color = Color.white;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_useChart)
            DrawFrameAndGridAndArea(vh);

        float half = Mathf.Max(0.5f, lineThicknessPixels * 0.5f);
        float lineHalf = half * 1.15f; // Slightly over grid so the trace reads on top
        Color32 c32 = _lineColor;
        if (_points.Count == 0)
            return;
        if (_points.Count == 1)
        {
            Vector2 c = _points[0];
            float r = Mathf.Max(4f, lineThicknessPixels * 1.4f);
            var p0 = c + new Vector2(-r, -r);
            var p1 = c + new Vector2(r, -r);
            var p2 = c + new Vector2(r, r);
            var p3 = c + new Vector2(-r, r);
            AddQuad4Colors(vh, p0, p1, p2, p3, c32, c32, c32, c32);
            return;
        }

        for (int i = 0; i < _points.Count - 1; i++)
            AddSegmentQuad(vh, _points[i], _points[i + 1], lineHalf, c32);
    }

    private void DrawFrameAndGridAndArea(VertexHelper vh)
    {
        float hw = _plotW * 0.5f;
        float hh = _plotH * 0.5f;
        float yBottom = -hh;
        const float borderPx = 1.1f;
        Color32 frameC = new Color32(90, 100, 115, 200);

        if (_showFrame)
        {
            // Rectangle frame (four thin quads)
            AddSegmentQuad(vh, new Vector2(-hw, -hh), new Vector2(hw, -hh), borderPx, frameC);
            AddSegmentQuad(vh, new Vector2(-hw, hh), new Vector2(hw, hh), borderPx, frameC);
            AddSegmentQuad(vh, new Vector2(-hw, -hh), new Vector2(-hw, hh), borderPx, frameC);
            AddSegmentQuad(vh, new Vector2(hw, -hh), new Vector2(hw, hh), borderPx, frameC);
        }

        if (_showArea && _points.Count >= 2)
        {
            Color32 top = _areaColorTop;
            Color32 bottom = _areaColorBottom;
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector2 a = _points[i];
                Vector2 b = _points[i + 1];
                var ab = new Vector2(a.x, yBottom);
                var bb = new Vector2(b.x, yBottom);
                AddQuad4Colors(vh, a, b, bb, ab, top, top, bottom, bottom);
            }
        }

        if (_showGrid)
        {
            var gridC = (Color32)_gridColor;
            for (int g = 0; g < _gridLinesH; g++)
            {
                float t = _gridLinesH == 1 ? 0.5f : g / (float)(_gridLinesH - 1);
                float y = -hh + t * _plotH;
                AddSegmentQuad(vh, new Vector2(-hw, y), new Vector2(hw, y), 0.6f, gridC);
            }
        }
    }

    private static void AddQuad4Colors(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
    {
        int i0 = vh.currentVertCount;
        vh.AddVert(UiVert(a.x, a.y, ca));
        vh.AddVert(UiVert(b.x, b.y, cb));
        vh.AddVert(UiVert(c.x, c.y, cc));
        vh.AddVert(UiVert(d.x, d.y, cd));
        vh.AddTriangle(i0, i0 + 1, i0 + 2);
        vh.AddTriangle(i0, i0 + 2, i0 + 3);
    }

    private static void AddSegmentQuad(VertexHelper vh, Vector2 a, Vector2 b, float halfWidth, Color32 c32)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.001f) return;
        dir /= len;
        Vector2 n = new Vector2(-dir.y, dir.x) * halfWidth;

        Vector2 p0 = a - n, p1 = a + n, p2 = b + n, p3 = b - n;
        var v0 = UiVert(p0.x, p0.y, c32);
        var v1 = UiVert(p1.x, p1.y, c32);
        var v2 = UiVert(p2.x, p2.y, c32);
        var v3 = UiVert(p3.x, p3.y, c32);

        int i0 = vh.currentVertCount;
        vh.AddVert(v0);
        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);
        vh.AddTriangle(i0, i0 + 1, i0 + 2);
        vh.AddTriangle(i0, i0 + 2, i0 + 3);
    }

    private static UIVertex UiVert(float x, float y, Color32 c32)
    {
        return new UIVertex
        {
            position = new Vector3(x, y, 0f),
            color = c32,
            uv0 = Vector2.zero,
            normal = Vector3.back,
            tangent = new Vector4(1f, 0f, 0f, -1f)
        };
    }
}
