using UnityEngine;

/// <summary>
/// Stress Change Index per project appendix: SCI = ((HRV_base - HRV_current) / HRV_base) * 100
/// </summary>
public static class StressChangeIndexCalculator
{
    public enum StressBand
    {
        Low,
        Moderate,
        High
    }

    public static float ComputeSciPercent(float hrvBaselineMs, float hrvCurrentMs)
    {
        if (hrvBaselineMs <= 0.01f)
            return 0f;
        return ((hrvBaselineMs - hrvCurrentMs) / hrvBaselineMs) * 100f;
    }

    public static StressBand Classify(float sciPercent)
    {
        if (sciPercent >= 50f) return StressBand.High;
        if (sciPercent >= 20f) return StressBand.Moderate;
        return StressBand.Low;
    }

    public static string BandLabel(StressBand band)
    {
        return band switch
        {
            StressBand.High => "High",
            StressBand.Moderate => "Moderate",
            StressBand.Low => "Low",
            _ => "-"
        };
    }

    public static Color GetBandColor(StressBand band)
    {
        return band switch
        {
            StressBand.High => new Color(0.95f, 0.32f, 0.28f, 1f),
            StressBand.Moderate => new Color(0.98f, 0.82f, 0.22f, 1f),
            _ => new Color(0.35f, 0.88f, 0.48f, 1f)
        };
    }

    public static Color GetBandColor(float sciPercent) => GetBandColor(Classify(sciPercent));
}
