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
        if (_points.Count < 2)
            return;

        float half = Mathf.Max(0.5f, lineThicknessPixels * 0.5f);
        Color32 c32 = _lineColor;
        for (int i = 0; i < _points.Count - 1; i++)
            AddSegmentQuad(vh, _points[i], _points[i + 1], half, c32);
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
