using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared texture-based line chart drawing (grid, axes, thick trace) used by live HR and results panels.
/// </summary>
public static class TextureLineChartRenderer
{
    [Serializable]
    public struct Style
    {
        public bool useTransparentBackground;
        public Color lineColor;
        public int chartLineWidth;
        public Color gridColor;
        public Color axisColor;
        public Color pointColor;
        public int plotLeft;
        public int plotRight;
        public int plotTop;
        public int plotBottom;
        public bool useStressBandLineColors;
        public bool drawStressBandZones;
        public Color stressBandLowColor;
        public Color stressBandModerateColor;
        public Color stressBandHighColor;
        public float stressBandModerateThreshold;
        public float stressBandHighThreshold;

        public static Style Default => new Style
        {
            useTransparentBackground = true,
            lineColor = new Color(0.3f, 0.75f, 0.95f, 1f),
            chartLineWidth = 3,
            gridColor = new Color(0.42f, 0.5f, 0.58f, 0.45f),
            axisColor = new Color(0.75f, 0.82f, 0.9f, 0.9f),
            pointColor = Color.white,
            plotLeft = 60,
            plotRight = 20,
            plotTop = 20,
            plotBottom = 50,
            useStressBandLineColors = false,
            drawStressBandZones = false,
            stressBandLowColor = new Color(0.35f, 0.88f, 0.48f, 1f),
            stressBandModerateColor = new Color(0.98f, 0.82f, 0.22f, 1f),
            stressBandHighColor = new Color(0.95f, 0.32f, 0.28f, 1f),
            stressBandModerateThreshold = 20f,
            stressBandHighThreshold = 50f
        };
    }

    public readonly struct MissionMarker
    {
        public readonly int SampleIndex;
        public readonly float SecondsFromStart;
        public readonly string Label;

        public MissionMarker(int sampleIndex, float secondsFromStart, string label)
        {
            SampleIndex = sampleIndex;
            SecondsFromStart = secondsFromStart;
            Label = label ?? string.Empty;
        }
    }

    public readonly struct TimeValuePoint
    {
        public readonly double SecondsFromStart;
        public readonly float Value;

        public TimeValuePoint(double secondsFromStart, float value)
        {
            SecondsFromStart = secondsFromStart;
            Value = value;
        }
    }

    public static Texture2D RenderIndexedSeries(
        IReadOnlyList<float> values,
        float sampleIntervalSeconds,
        int width,
        int height,
        Style style)
    {
        if (values == null || values.Count < 2)
            return RenderEmpty(width, height, style);

        var points = new List<TimeValuePoint>(values.Count);
        for (int i = 0; i < values.Count; i++)
            points.Add(new TimeValuePoint(i * Math.Max(0.001f, sampleIntervalSeconds), values[i]));

        double duration = Math.Max(1.0, points[points.Count - 1].SecondsFromStart);
        return RenderTimeSeries(points, duration, width, height, style);
    }

    public static Texture2D RenderTimeSeries(
        IReadOnlyList<TimeValuePoint> points,
        double durationSeconds,
        int width,
        int height,
        Style style)
    {
        if (points == null || points.Count < 2 || width < 8 || height < 8)
            return RenderEmpty(width, height, style);

        durationSeconds = Math.Max(1.0, durationSeconds);

        float minValue = points[0].Value;
        float maxValue = points[0].Value;
        for (int i = 1; i < points.Count; i++)
        {
            minValue = Mathf.Min(minValue, points[i].Value);
            maxValue = Mathf.Max(maxValue, points[i].Value);
        }

        ComputeYRange(minValue, maxValue, out float yMin, out float yMax);
        return RenderTimeSeries(points, durationSeconds, width, height, style, yMin, yMax);
    }

    public static Texture2D RenderTimeSeries(
        IReadOnlyList<TimeValuePoint> points,
        double durationSeconds,
        int width,
        int height,
        Style style,
        float yMin,
        float yMax)
    {
        return RenderTimeSeries(points, durationSeconds, width, height, style, yMin, yMax, null);
    }

    public static Texture2D RenderTimeSeries(
        IReadOnlyList<TimeValuePoint> points,
        double durationSeconds,
        int width,
        int height,
        Style style,
        float yMin,
        float yMax,
        IReadOnlyList<MissionMarker> missionMarkers)
    {
        if (points == null || points.Count < 2 || width < 8 || height < 8)
            return RenderEmpty(width, height, style);

        durationSeconds = Math.Max(1.0, durationSeconds);

        var texture = CreateTexture(width, height);
        FillTexture(texture, BackgroundColor(style));

        int plotLeft = style.plotLeft;
        int plotRight = width - style.plotRight;
        int plotBottom = style.plotBottom;
        int plotTop = height - style.plotTop;
        int plotWidth = plotRight - plotLeft;
        int plotHeight = plotTop - plotBottom;

        if (style.drawStressBandZones)
            DrawStressBandZones(texture, plotLeft, plotRight, plotBottom, plotTop, yMin, yMax, style);

        DrawGrid(texture, plotLeft, plotRight, plotBottom, plotTop, style.gridColor);
        DrawLine(texture, plotLeft, plotBottom, plotRight, plotBottom, style.axisColor);
        DrawLine(texture, plotLeft, plotBottom, plotLeft, plotTop, style.axisColor);

        Vector2Int? previous = null;
        for (int i = 0; i < points.Count; i++)
        {
            TimeValuePoint point = points[i];
            float xNorm = Mathf.Clamp01((float)(point.SecondsFromStart / durationSeconds));
            float yNorm = Mathf.Clamp01((point.Value - yMin) / (yMax - yMin));

            int x = plotLeft + Mathf.RoundToInt(xNorm * plotWidth);
            int y = plotBottom + Mathf.RoundToInt(yNorm * plotHeight);
            var current = new Vector2Int(x, y);

            if (previous.HasValue)
            {
                float segmentValue = Mathf.Max(points[i - 1].Value, point.Value);
                Color32 lineColor = style.useStressBandLineColors
                    ? ResolveStressBandColor(segmentValue, style)
                    : style.lineColor;
                DrawThickLine(texture, previous.Value.x, previous.Value.y, current.x, current.y, lineColor, style.chartLineWidth);
            }

            Color32 pointColor = style.useStressBandLineColors
                ? ResolveStressBandColor(point.Value, style)
                : style.pointColor;
            DrawMarkerPoint(texture, x, y, pointColor);
            previous = current;
        }

        if (missionMarkers != null && missionMarkers.Count > 0)
            DrawMissionMarkers(texture, points, missionMarkers, durationSeconds, plotLeft, plotRight, plotBottom, plotTop, yMin, yMax);

        texture.Apply();
        return texture;
    }

    public static Texture2D RenderEmpty(int width, int height, Style style)
    {
        width = Mathf.Max(8, width);
        height = Mathf.Max(8, height);

        var texture = CreateTexture(width, height);
        FillTexture(texture, BackgroundColor(style));

        int plotLeft = style.plotLeft;
        int plotRight = width - style.plotRight;
        int plotBottom = style.plotBottom;
        int plotTop = height - style.plotTop;

        DrawGrid(texture, plotLeft, plotRight, plotBottom, plotTop, style.gridColor);
        DrawLine(texture, plotLeft, plotBottom, plotRight, plotBottom, style.axisColor);
        DrawLine(texture, plotLeft, plotBottom, plotLeft, plotTop, style.axisColor);

        texture.Apply();
        return texture;
    }

    public static void ComputeYRange(float minValue, float maxValue, out float yMin, out float yMax)
    {
        yMin = minValue;
        yMax = maxValue;
        float range = yMax - yMin;
        float padding = Mathf.Max(5f, range * 0.15f);
        yMin = Mathf.Max(0f, yMin - padding);
        yMax = yMax + padding;

        if (Mathf.Approximately(yMin, yMax))
        {
            yMin -= 5f;
            yMax += 5f;
        }
    }

    private static Color32 BackgroundColor(Style style) =>
        style.useTransparentBackground
            ? new Color32(0, 0, 0, 0)
            : new Color32(18, 18, 18, 255);

    private static Texture2D CreateTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private static void DrawGrid(Texture2D texture, int plotLeft, int plotRight, int plotBottom, int plotTop, Color32 grid)
    {
        int plotWidth = plotRight - plotLeft;
        int plotHeight = plotTop - plotBottom;

        for (int i = 0; i <= 5; i++)
        {
            int x = plotLeft + Mathf.RoundToInt(plotWidth * (i / 5f));
            DrawLine(texture, x, plotBottom, x, plotTop, grid);
        }

        for (int i = 0; i <= 5; i++)
        {
            int y = plotBottom + Mathf.RoundToInt(plotHeight * (i / 5f));
            DrawLine(texture, plotLeft, y, plotRight, y, grid);
        }
    }

    private static void FillTexture(Texture2D texture, Color32 color)
    {
        Color32[] pixels = new Color32[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        texture.SetPixels32(pixels);
    }

    private static void DrawSmallPoint(Texture2D texture, int centerX, int centerY, Color32 color)
    {
        DrawMarkerPoint(texture, centerX, centerY, color, 1);
    }

    private static void DrawMarkerPoint(Texture2D texture, int centerX, int centerY, Color32 color, int radius = 3)
    {
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= radius * radius + radius)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static Color32 ResolveStressBandColor(float sciValue, Style style)
    {
        if (sciValue >= style.stressBandHighThreshold)
            return style.stressBandHighColor;
        if (sciValue >= style.stressBandModerateThreshold)
            return style.stressBandModerateColor;
        return style.stressBandLowColor;
    }

    private static void DrawStressBandZones(
        Texture2D texture,
        int plotLeft,
        int plotRight,
        int plotBottom,
        int plotTop,
        float yMin,
        float yMax,
        Style style)
    {
        FillBandZone(texture, plotLeft, plotRight, plotBottom, plotTop, yMin, yMax, yMin, style.stressBandModerateThreshold, WithAlpha(style.stressBandLowColor, 0.12f));
        FillBandZone(texture, plotLeft, plotRight, plotBottom, plotTop, yMin, yMax, style.stressBandModerateThreshold, style.stressBandHighThreshold, WithAlpha(style.stressBandModerateColor, 0.12f));
        FillBandZone(texture, plotLeft, plotRight, plotBottom, plotTop, yMin, yMax, style.stressBandHighThreshold, yMax, WithAlpha(style.stressBandHighColor, 0.14f));
    }

    private static void FillBandZone(
        Texture2D texture,
        int plotLeft,
        int plotRight,
        int plotBottom,
        int plotTop,
        float yMin,
        float yMax,
        float bandMinValue,
        float bandMaxValue,
        Color32 fill)
    {
        float lowNorm = Mathf.Clamp01((bandMinValue - yMin) / (yMax - yMin));
        float highNorm = Mathf.Clamp01((bandMaxValue - yMin) / (yMax - yMin));
        int yLow = plotBottom + Mathf.RoundToInt(lowNorm * (plotTop - plotBottom));
        int yHigh = plotBottom + Mathf.RoundToInt(highNorm * (plotTop - plotBottom));
        int bottom = Mathf.Min(yLow, yHigh);
        int top = Mathf.Max(yLow, yHigh);

        for (int y = bottom; y <= top; y++)
        {
            for (int x = plotLeft; x <= plotRight; x++)
                SetPixelSafe(texture, x, y, fill);
        }
    }

    private static Color32 WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void DrawMissionMarkers(
        Texture2D texture,
        IReadOnlyList<TimeValuePoint> points,
        IReadOnlyList<MissionMarker> missionMarkers,
        double durationSeconds,
        int plotLeft,
        int plotRight,
        int plotBottom,
        int plotTop,
        float yMin,
        float yMax)
    {
        int plotWidth = plotRight - plotLeft;
        int plotHeight = plotTop - plotBottom;
        var markerLine = new Color32(255, 220, 120, 140);
        var markerRing = new Color32(20, 24, 32, 230);
        var markerDot = new Color32(255, 196, 64, 255);

        for (int i = 0; i < missionMarkers.Count; i++)
        {
            MissionMarker marker = missionMarkers[i];
            int sampleIndex = Mathf.Clamp(marker.SampleIndex, 0, points.Count - 1);
            TimeValuePoint point = points[sampleIndex];

            float seconds = marker.SecondsFromStart > 0f
                ? marker.SecondsFromStart
                : (float)point.SecondsFromStart;
            float xNorm = Mathf.Clamp01((float)(seconds / durationSeconds));
            float yNorm = Mathf.Clamp01((point.Value - yMin) / (yMax - yMin));
            int x = plotLeft + Mathf.RoundToInt(xNorm * plotWidth);
            int y = plotBottom + Mathf.RoundToInt(yNorm * plotHeight);

            DrawDashedVerticalLine(texture, x, plotBottom, plotTop, markerLine);
            DrawMarkerPoint(texture, x, y, markerRing, 6);
            DrawMarkerPoint(texture, x, y, markerDot, 4);
        }
    }

    private static void DrawDashedVerticalLine(Texture2D texture, int x, int y0, int y1, Color32 color)
    {
        if (y1 < y0)
            (y0, y1) = (y1, y0);

        for (int y = y0; y <= y1; y += 4)
            DrawLine(texture, x, y, x, Mathf.Min(y + 2, y1), color);
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetPixelSafe(texture, x0, y0, color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private static void DrawThickLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color, int thickness)
    {
        if (thickness <= 1)
        {
            DrawLine(texture, x0, y0, x1, y1, color);
            return;
        }

        int radius = (thickness - 1) / 2;
        for (int oy = -radius; oy <= radius; oy++)
        {
            for (int ox = -radius; ox <= radius; ox++)
                DrawLine(texture, x0 + ox, y0 + oy, x1 + ox, y1 + oy, color);
        }
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
            return;
        texture.SetPixel(x, y, color);
    }
}
