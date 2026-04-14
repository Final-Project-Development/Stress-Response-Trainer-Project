using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Personalized resilience tips derived from SCI bands (per project book FR 7.2).
/// </summary>
public static class StressRecommendations
{
    /// <summary>One-line SCI summary (mean + peak bands). Empty history returns a short placeholder.</summary>
    public static string BuildStatsSummary(IReadOnlyList<float> sciHistory)
    {
        if (sciHistory == null || sciHistory.Count == 0)
            return "No SCI samples recorded for this session.";

        float peak = sciHistory.Max();
        float mean = sciHistory.Average();
        var peakBand = StressChangeIndexCalculator.Classify(peak);
        var meanBand = StressChangeIndexCalculator.Classify(mean);
        return
            $"Average SCI: {mean:F1}% ({StressChangeIndexCalculator.BandLabel(meanBand)}). Peak: {peak:F1}% ({StressChangeIndexCalculator.BandLabel(peakBand)}).";
    }

    /// <summary>Behavioral tips only (no numeric summary). Suited for a dedicated “recommendations” column.</summary>
    public static string BuildBehavioralTips(IReadOnlyList<float> sciHistory)
    {
        if (sciHistory == null || sciHistory.Count == 0)
            return "Complete another session to receive tailored feedback.";

        float peak = sciHistory.Max();
        var peakBand = StressChangeIndexCalculator.Classify(peak);

        var sb = new System.Text.StringBuilder();
        if (peakBand == StressChangeIndexCalculator.StressBand.High)
        {
            sb.AppendLine("Under high load, try box breathing (4s in, 4s hold, 4s out) between tasks.");
            sb.AppendLine("Practice naming three objects you see — it can help re-engage prefrontal control.");
        }
        else if (peakBand == StressChangeIndexCalculator.StressBand.Moderate)
        {
            sb.AppendLine("Moderate stress response: keep a steady pace; prioritize one clear action at a time.");
            sb.AppendLine("Short grounding breaks after alarms can speed recovery toward baseline HRV.");
        }
        else
        {
            sb.AppendLine("Stress profile stayed relatively low — good regulation. Add time pressure in future runs to train harder scenarios.");
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildFromSciHistory(IReadOnlyList<float> sciHistory)
    {
        if (sciHistory == null || sciHistory.Count == 0)
            return "Complete another session to receive tailored feedback.";

        return $"{BuildStatsSummary(sciHistory)}\n\n{BuildBehavioralTips(sciHistory)}".TrimEnd();
    }

    public static string BeforeNextStageBreathingTip()
    {
        return "Before the next stage: breathe deeply through the nose, lengthen the exhale, and try to bring arousal down toward your baseline.";
    }
}
