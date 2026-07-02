using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds SCI/HRV history from real watch heart-rate samples (live or post-workout timeline).
/// </summary>
public static class WatchSessionResultsImporter
{
    public struct ImportedWatchResults
    {
        public List<float> SciPercent;
        public List<float> HrvMs;
        public float BaselineHrvMs;
        public bool HasData;
    }

    public static ImportedWatchResults BuildFromHeartRates(
        IReadOnlyList<float> heartRatesBpm,
        float lockedBaselineHrvMs,
        float nominalRestHeartRate,
        float nominalRestHrvMs)
    {
        var result = new ImportedWatchResults
        {
            SciPercent = new List<float>(),
            HrvMs = new List<float>(),
            BaselineHrvMs = lockedBaselineHrvMs,
            HasData = false
        };

        if (heartRatesBpm == null || heartRatesBpm.Count == 0)
            return result;

        float baseline = lockedBaselineHrvMs;
        if (baseline <= 0.01f)
            baseline = EstimateRestBaselineFromHeartRates(heartRatesBpm, nominalRestHeartRate, nominalRestHrvMs);

        result.BaselineHrvMs = baseline;

        for (int i = 0; i < heartRatesBpm.Count; i++)
        {
            float hr = heartRatesBpm[i];
            if (hr <= 0f)
                continue;

            float hrv = MockPhysiologySource.EstimateHrvMsFromHeartRate(hr, nominalRestHeartRate, nominalRestHrvMs);
            float sci = StressChangeIndexCalculator.ComputeSciPercent(baseline, hrv);
            result.HrvMs.Add(hrv);
            result.SciPercent.Add(sci);
        }

        result.HasData = result.SciPercent.Count > 0;
        return result;
    }

    static float EstimateRestBaselineFromHeartRates(
        IReadOnlyList<float> heartRatesBpm,
        float nominalRestHeartRate,
        float nominalRestHrvMs)
    {
        int restSampleCount = Mathf.Max(1, heartRatesBpm.Count / 4);
        float lowestHrSum = 0f;
        int count = 0;

        for (int i = 0; i < restSampleCount && i < heartRatesBpm.Count; i++)
        {
            float hr = heartRatesBpm[i];
            if (hr <= 0f)
                continue;

            lowestHrSum += hr;
            count++;
        }

        if (count == 0)
            return nominalRestHrvMs;

        float avgRestHr = lowestHrSum / count;
        return MockPhysiologySource.EstimateHrvMsFromHeartRate(avgRestHr, nominalRestHeartRate, nominalRestHrvMs);
    }
}
