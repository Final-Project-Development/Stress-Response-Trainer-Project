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
}
