using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Personalized resilience tips derived from SCI bands and stored session history (FR 7.2).
/// </summary>
public static class StressRecommendations
{
    public enum SimulationStage
    {
        Sim1,
        Sim2
    }

    const float TrendThresholdPercent = 5f;
    const float RecoveryTrendThresholdSeconds = 3f;
    const float TimeTrendThresholdSeconds = 15f;
    const float HighStressSciThreshold = 50f;

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

        var sb = new StringBuilder();
        if (peakBand == StressChangeIndexCalculator.StressBand.High)
        {
            sb.AppendLine("Under high load, try box breathing (4s in, 4s hold, 4s out) between tasks.");
            sb.AppendLine("Practice naming three objects you see. This can help re-engage prefrontal control.");
        }
        else if (peakBand == StressChangeIndexCalculator.StressBand.Moderate)
        {
            sb.AppendLine("Moderate stress response: keep a steady pace. prioritize one clear action at a time.");
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

    public static string BuildPersonalizedRecommendations(
        IReadOnlyList<float> sciHistory,
        SimulationStage stage,
        SimulationRunOutcome outcome = null,
        float recoverySeconds = -1f)
    {
        if (sciHistory == null || sciHistory.Count == 0)
            return "Complete another session to receive tailored feedback.";

        float peak = sciHistory.Max();
        float mean = sciHistory.Average();
        var sb = new StringBuilder();

        string progress = BuildProgressSummary(peak, mean, recoverySeconds, stage, outcome);
        if (!string.IsNullOrEmpty(progress))
        {
            sb.AppendLine("<b>Your progress</b>");
            sb.AppendLine(progress);
            sb.AppendLine();
        }

        sb.AppendLine("<b>This run</b>");
        sb.AppendLine(BuildBehavioralTips(sciHistory));

        if (stage == SimulationStage.Sim2)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Session comparison</b>");
            sb.AppendLine(SessionHistoryStore.BuildPhysiologicalRecoverySummary(peak, mean));
        }

        string nextStep = BuildNextStepAdvice(peak, stage, outcome);
        if (!string.IsNullOrEmpty(nextStep))
        {
            sb.AppendLine();
            sb.AppendLine("<b>Recommended next step</b>");
            sb.AppendLine(nextStep);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Short guidance for the level-select screen.</summary>
    public static string BuildLevelSelectGuidance()
    {
        SessionHistoryStore.TryGetCurrentSession(out var current);
        var priorSim1 = SessionHistoryStore.GetLatestPriorSim1();
        var priorSim2 = SessionHistoryStore.GetLatestPriorSim2();

        bool sim1ThisSession = HasSim1Data(current);
        bool sim2ThisSession = HasSim2Data(current);
        bool sim1Before = priorSim1 != null;
        bool sim2Before = priorSim2 != null;

        if (!sim1ThisSession && !sim2ThisSession && !sim1Before && !sim2Before)
            return "Start with an Environment Learning tour, then try Simulation 1.";

        if (sim2ThisSession && (sim1ThisSession || sim1Before))
            return BuildBothStagesCompletedGuidance(
                sim1ThisSession ? current.sim1PeakSci : priorSim1.sim1PeakSci,
                current.sim2PeakSci,
                sameSession: sim1ThisSession,
                sim2Outcome: SessionHistoryStore.BuildOutcomeFromRecord(current, SimulationStage.Sim2));

        if (sim1ThisSession && current.sim1Disqualified)
            return "Simulation 1 ended in disqualification — restart and finish each step within its time limit.";

        if (sim2ThisSession && current.sim2Disqualified)
            return "Simulation 2 ended in disqualification — restart and keep a steady pace on every task.";

        if (sim1ThisSession && current.sim1TimedOut)
            return "Simulation 1 timed out: retry with a steady pace and finish all steps before the clock runs out.";

        if (sim2ThisSession && current.sim2TimedOut)
            return "Simulation 2 timed out: repeat it and prioritize kit, contact and treatment in order.";

        if (sim1ThisSession && current.sim1MissionCompleted && current.sim1DurationSeconds > 0f &&
            current.sim1TimeLimitSeconds > 0f &&
            current.sim1DurationSeconds <= current.sim1TimeLimitSeconds * 0.5f)
            return "Strong pace on Simulation 1: continue to Simulation 2 when ready.";

        if (sim2Before && sim1Before)
        {
            float peakDelta = priorSim2.sim2PeakSci - priorSim1.sim1PeakSci;
            if (peakDelta > TrendThresholdPercent)
                return "In your last full session, stress rose in Simulation 2. Take a breathing break between stages on the next run.";
            if (priorSim2.sim2PeakSci < priorSim1.sim1PeakSci - TrendThresholdPercent)
                return "You recovered well in your last full session. Repeat Simulation 2 to reinforce that pattern.";
            return "You have completed both simulations before. Repeat either stage or take an Environment Learning tour.";
        }

        float latestSim1Peak = sim1ThisSession ? current.sim1PeakSci : priorSim1?.sim1PeakSci ?? 0f;
        if (latestSim1Peak >= 50f)
            return "Your last Simulation 1 showed high stress. Take an Environment Learning tour, then retry with calming breaths.";

        if (sim1ThisSession || sim1Before)
            return "Good progress on Simulation 1. Continue to Simulation 2.";

        return "Pick the stage you have practiced least recently, or take an Environment Learning tour to refresh.";
    }

    static string BuildBothStagesCompletedGuidance(
        float sim1PeakSci,
        float sim2PeakSci,
        bool sameSession,
        SimulationRunOutcome sim2Outcome = null)
    {
        float peakDelta = sim2PeakSci - sim1PeakSci;
        string when = sameSession ? "today" : "recently";

        if (sim2Outcome != null && sim2Outcome.timedOut)
            return $"Simulation 2 {when} ran out of time — repeat it with a clearer step-by-step plan.";

        if (sim2Outcome != null && sim2Outcome.WasFast)
            return $"You finished both stages {when} with good pace — repeat either simulation to reinforce the pattern.";

        if (peakDelta > TrendThresholdPercent)
            return $"You finished both stages {when}. Stress rose in Simulation 2 — take breathing breaks between stages, or repeat either simulation.";

        if (sim2PeakSci < sim1PeakSci - TrendThresholdPercent)
            return $"You finished both stages {when} with strong recovery. Repeat Simulation 2 or refresh with Environment Learning.";

        return $"You finished both stages {when}. Repeat either simulation to practice, or take an Environment Learning tour.";
    }

    static bool HasSim1Data(SessionHistoryStore.SessionRecord record) =>
        record != null && record.sim1Samples > 0;

    static bool HasSim2Data(SessionHistoryStore.SessionRecord record) =>
        record != null && record.sim2Samples > 0;

    public static string BuildResultsTabMetrics(
        SimulationStage stage,
        float peakSci,
        float meanSci,
        SimulationRunOutcome outcome = null,
        float baselineHrvMs = 0f,
        float minHrvMs = 0f,
        float maxHrvMs = 0f,
        float avgHrvMs = 0f,
        IReadOnlyList<float> sciHistory = null,
        float sampleIntervalSeconds = 0.4f,
        SimulationResultsPanelsConfig display = null)
    {
        ResultsPanelDisplayOptions options = display?.results ?? ResultsPanelDisplayOptions.AllEnabled();
        var peakBand = StressChangeIndexCalculator.Classify(peakSci);
        string band = StressChangeIndexCalculator.BandLabel(peakBand);
        var sb = new StringBuilder();

        if (options.showRunTimeAndStatus)
        {
            string timeLine = BuildTimeMetricsLine(outcome);
            if (!string.IsNullOrEmpty(timeLine))
                sb.AppendLine(timeLine);
        }

        if (options.showMissionPaceComparison)
        {
            var prior = stage == SimulationStage.Sim1
                ? SessionHistoryStore.GetLatestPriorSim1()
                : SessionHistoryStore.GetLatestPriorSim2();
            string paceLine = BuildTimeProgressComparison(outcome, prior, stage);
            if (!string.IsNullOrEmpty(paceLine))
                sb.AppendLine(paceLine);
        }

        if (options.showHighStressTime)
        {
            float highStressSeconds = outcome != null && outcome.highStressSeconds > 0f
                ? outcome.highStressSeconds
                : ComputeHighStressSeconds(sciHistory, sampleIntervalSeconds);
            if (highStressSeconds > 0f)
            {
                string stressTimeLine = $"High-stress time: {highStressSeconds:F0}s (SCI ≥ {HighStressSciThreshold:F0}%)";
                var prior = stage == SimulationStage.Sim1
                    ? SessionHistoryStore.GetLatestPriorSim1()
                    : SessionHistoryStore.GetLatestPriorSim2();
                if (prior != null)
                {
                    float priorHighStress = PriorHighStressSeconds(prior, stage);
                    if (priorHighStress > 0f)
                    {
                        float delta = highStressSeconds - priorHighStress;
                        if (Mathf.Abs(delta) >= TimeTrendThresholdSeconds)
                        {
                            stressTimeLine += delta < 0f
                                ? $" — {delta:F0}s vs last run"
                                : $" — +{delta:F0}s vs last run";
                        }
                    }
                }

                sb.AppendLine(stressTimeLine);
            }
        }

        if (stage == SimulationStage.Sim1)
        {
            if (options.showBaselineHrv)
                sb.AppendLine($"Baseline HRV: {baselineHrvMs:F0} ms");
            if (options.showPeakSci)
                sb.AppendLine($"Peak SCI: {peakSci:F0}% ({band})");
            if (options.showAvgSci)
                sb.AppendLine($"Avg SCI: {meanSci:F0}%");
            return sb.ToString().TrimEnd();
        }

        if (options.showPeakSci)
            sb.AppendLine($"Peak SCI: {peakSci:F0}% ({band})");
        if (options.showAvgSci)
            sb.AppendLine($"Avg SCI: {meanSci:F0}%");
        if (options.showHrvRange && minHrvMs > 0f && maxHrvMs > 0f)
            sb.AppendLine($"HRV: {minHrvMs:F0}–{maxHrvMs:F0} ms (avg {avgHrvMs:F0})");

        return sb.ToString().TrimEnd();
    }

    /// <summary>Recommendations tab only — configurable tips and next steps.</summary>
    public static string BuildRecommendationsTabOnly(
        IReadOnlyList<float> sciHistory,
        SimulationStage stage,
        SimulationRunOutcome outcome = null,
        SimulationResultsPanelsConfig display = null)
    {
        RecommendationsPanelDisplayOptions options =
            display?.recommendations ?? RecommendationsPanelDisplayOptions.AllEnabled();

        if (sciHistory == null || sciHistory.Count == 0)
            return BuildEmptyRunFeedback(outcome, stage, options);

        float peak = sciHistory.Max();
        float mean = sciHistory.Average();
        var sb = new StringBuilder();

        if (options.showTimeAdvice)
        {
            string timeTip = BuildTimeAdvice(outcome, stage);
            if (!string.IsNullOrEmpty(timeTip))
                sb.AppendLine(timeTip);
        }

        if (options.showProgressHint)
        {
            string progressHint = FirstLine(BuildProgressSummary(peak, mean, -1f, stage, outcome));
            if (!string.IsNullOrEmpty(progressHint) &&
                (progressHint.IndexOf("pace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 progressHint.IndexOf("stress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 progressHint.IndexOf("SCI", StringComparison.Ordinal) >= 0))
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.AppendLine(progressHint);
            }
        }

        if (options.showBehavioralTip)
        {
            string firstTip = FirstLine(BuildBehavioralTips(sciHistory));
            if (!string.IsNullOrEmpty(firstTip))
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.AppendLine(firstTip);
            }
        }

        if (options.showSessionComparison && stage == SimulationStage.Sim2)
        {
            string comparison = FirstLine(SessionHistoryStore.BuildPhysiologicalRecoverySummary(peak, mean));
            if (!string.IsNullOrEmpty(comparison))
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.AppendLine(comparison);
            }
        }

        if (options.showNextStep)
        {
            string next = BuildNextStepAdvice(peak, stage, outcome);
            if (!string.IsNullOrEmpty(next))
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.AppendLine(next);
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildCompactResultsRecommendations(
        IReadOnlyList<float> sciHistory,
        SimulationStage stage,
        SimulationRunOutcome outcome = null,
        float recoverySeconds = -1f)
    {
        if (sciHistory == null || sciHistory.Count == 0)
            return "Complete another session to receive tailored feedback.";

        float peak = sciHistory.Max();
        float mean = sciHistory.Average();
        var sb = new StringBuilder();

        string progress = BuildProgressSummary(peak, mean, recoverySeconds, stage, outcome);
        if (!string.IsNullOrEmpty(progress))
        {
            sb.AppendLine(FirstLine(progress));
            sb.AppendLine();
        }

        foreach (string line in BuildBehavioralTips(sciHistory).Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine("- " + line.Trim());
        }

        if (stage == SimulationStage.Sim2)
        {
            string comparison = SessionHistoryStore.BuildPhysiologicalRecoverySummary(peak, mean);
            string trend = LastNonEmptyLine(comparison);
            if (!string.IsNullOrEmpty(trend))
            {
                sb.AppendLine();
                sb.AppendLine(trend);
            }
        }

        string next = BuildNextStepAdvice(peak, stage);
        if (!string.IsNullOrEmpty(next))
        {
            sb.AppendLine();
            sb.AppendLine("Next: " + next);
        }

        return sb.ToString().TrimEnd();
    }

    public static string ResultsTabFooterLine() =>
        "Use Choose Simulation to pick your next scenario.";

    static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        int index = text.IndexOf('\n');
        return index >= 0 ? text.Substring(0, index).Trim() : text.Trim();
    }

    static string LastNonEmptyLine(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var lines = text.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return lines[i].Trim();
        }

        return string.Empty;
    }

    public static string BeforeNextStageBreathingTip()
    {
        return "Before the next stage: breathe deeply through the nose, lengthen the exhale, and try to bring arousal down toward your baseline.";
    }

    static string BuildProgressSummary(
        float peakSci,
        float meanSci,
        float recoverySeconds,
        SimulationStage stage,
        SimulationRunOutcome outcome = null)
    {
        int priorCount = SessionHistoryStore.CountPriorSessions();
        if (priorCount == 0)
            return "First saved session for this profile — future runs will compare against today's baseline.";

        var prior = stage == SimulationStage.Sim1
            ? SessionHistoryStore.GetLatestPriorSim1()
            : SessionHistoryStore.GetLatestPriorSim2();

        if (prior == null)
            return $"You have {priorCount} earlier session(s), but no prior {StageLabel(stage)} data yet — this run sets your personal benchmark.";

        var sb = new StringBuilder();
        float peakDelta = peakSci - priorPeak(prior, stage);
        string direction = peakDelta switch
        {
            < -TrendThresholdPercent => $"Peak SCI improved by {Math.Abs(peakDelta):F1}% compared to your last {StageLabel(stage)} ({priorPeak(prior, stage):F1}% → {peakSci:F1}%).",
            > TrendThresholdPercent => $"Peak SCI rose by {peakDelta:F1}% compared to your last {StageLabel(stage)} ({priorPeak(prior, stage):F1}% → {peakSci:F1}%).",
            _ => $"Peak SCI is similar to your last {StageLabel(stage)} ({priorPeak(prior, stage):F1}% vs {peakSci:F1}% now)."
        };
        sb.AppendLine(direction);

        float priorMean = stage == SimulationStage.Sim1 ? prior.sim1MeanSci : prior.sim2MeanSci;
        if (priorMean > 0.01f)
        {
            float meanDelta = meanSci - priorMean;
            if (Math.Abs(meanDelta) >= TrendThresholdPercent)
            {
                sb.AppendLine(meanDelta < 0f
                    ? $"Average stress load also dropped ({priorMean:F1}% → {meanSci:F1}%)."
                    : $"Average stress load increased ({priorMean:F1}% → {meanSci:F1}%).");
            }
        }

        string paceLine = BuildTimeProgressComparison(outcome, prior, stage);
        if (!string.IsNullOrEmpty(paceLine))
            sb.AppendLine(paceLine);

        float priorHighStress = PriorHighStressSeconds(prior, stage);
        if (outcome != null && outcome.highStressSeconds > 0f && priorHighStress > 0f)
        {
            float highStressDelta = outcome.highStressSeconds - priorHighStress;
            if (highStressDelta <= -TimeTrendThresholdSeconds)
                sb.AppendLine($"High-stress exposure dropped ({priorHighStress:F0}s → {outcome.highStressSeconds:F0}s at SCI ≥ {HighStressSciThreshold:F0}%).");
            else if (highStressDelta >= TimeTrendThresholdSeconds)
                sb.AppendLine($"High-stress exposure lasted longer ({priorHighStress:F0}s → {outcome.highStressSeconds:F0}s).");
        }

        float priorRecovery = stage == SimulationStage.Sim1 ? prior.sim1RecoverySeconds : prior.sim2RecoverySeconds;
        if (recoverySeconds >= 0f && priorRecovery >= 0f)
        {
            float recoveryDelta = recoverySeconds - priorRecovery;
            if (recoveryDelta <= -RecoveryTrendThresholdSeconds)
                sb.AppendLine($"Recovery to low stress was faster ({priorRecovery:F0}s → {recoverySeconds:F0}s).");
            else if (recoveryDelta >= RecoveryTrendThresholdSeconds)
                sb.AppendLine($"Recovery took longer ({priorRecovery:F0}s → {recoverySeconds:F0}s) — try a short breathing reset after alarms.");
        }

        var recentHighRuns = SessionHistoryStore.GetPriorSessions(null, 5)
            .Count(s => PeakForStage(s, stage) >= 50f);
        if (recentHighRuns >= 2 && peakSci >= 50f)
            sb.AppendLine("High stress appeared in several recent runs — prioritize grounding before starting the next mission.");

        return sb.ToString().TrimEnd();
    }

    public static float ComputeHighStressSeconds(
        IReadOnlyList<float> sciHistory,
        float sampleIntervalSeconds,
        float thresholdPercent = HighStressSciThreshold)
    {
        if (sciHistory == null || sciHistory.Count == 0 || sampleIntervalSeconds <= 0f)
            return 0f;

        int highSamples = 0;
        for (int i = 0; i < sciHistory.Count; i++)
        {
            if (sciHistory[i] >= thresholdPercent)
                highSamples++;
        }

        return highSamples * sampleIntervalSeconds;
    }

    static string BuildTimeProgressComparison(
        SimulationRunOutcome outcome,
        SessionHistoryStore.SessionRecord prior,
        SimulationStage stage)
    {
        if (outcome == null || prior == null || !HasPriorTimeData(prior, stage))
            return string.Empty;

        float priorDuration = PriorDurationSeconds(prior, stage);
        bool priorTimedOut = PriorTimedOut(prior, stage);
        bool priorCompleted = PriorMissionCompleted(prior, stage);
        float priorProgress = PriorMissionProgress(prior, stage);

        if (outcome.timedOut && priorCompleted)
            return "Mission pace: did not finish this time (you completed it last run).";

        if (outcome.missionCompleted && priorTimedOut)
            return "Mission pace: finished this time (last run timed out).";

        if (outcome.timedOut && priorTimedOut)
        {
            float delta = outcome.completionRatio - priorProgress;
            if (delta >= 0.1f)
                return $"Mission pace: reached {Mathf.RoundToInt(outcome.completionRatio * 100f)}% vs {Mathf.RoundToInt(priorProgress * 100f)}% last run before time ran out.";
            if (delta <= -0.1f)
                return "Mission pace: less progress than last run before time ran out.";
            return "Mission pace: similar progress to your last timed-out run.";
        }

        if (outcome.missionCompleted && priorCompleted && priorDuration > 0f)
        {
            float delta = outcome.elapsedSeconds - priorDuration;
            if (delta <= -TimeTrendThresholdSeconds)
                return $"Mission pace: {FormatDuration(Mathf.Abs(delta))} faster than last run.";
            if (delta >= TimeTrendThresholdSeconds)
                return $"Mission pace: {FormatDuration(delta)} slower than last run.";
        }

        return string.Empty;
    }

    static bool HasPriorTimeData(SessionHistoryStore.SessionRecord prior, SimulationStage stage) =>
        PriorDurationSeconds(prior, stage) > 0f ||
        PriorTimedOut(prior, stage) ||
        PriorMissionCompleted(prior, stage);

    static float PriorDurationSeconds(SessionHistoryStore.SessionRecord prior, SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? prior.sim1DurationSeconds : prior.sim2DurationSeconds;

    static bool PriorTimedOut(SessionHistoryStore.SessionRecord prior, SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? prior.sim1TimedOut : prior.sim2TimedOut;

    static bool PriorMissionCompleted(SessionHistoryStore.SessionRecord prior, SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? prior.sim1MissionCompleted : prior.sim2MissionCompleted;

    static float PriorMissionProgress(SessionHistoryStore.SessionRecord prior, SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? prior.sim1MissionProgress : prior.sim2MissionProgress;

    static float PriorHighStressSeconds(SessionHistoryStore.SessionRecord prior, SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? prior.sim1HighStressSeconds : prior.sim2HighStressSeconds;

    static string BuildNextStepAdvice(float peakSci, SimulationStage stage, SimulationRunOutcome outcome = null)
    {
        if (outcome != null && outcome.disqualified)
        {
            if (stage == SimulationStage.Sim1)
                return "Restart Simulation 1 — each task has its own time limit; 3 slow steps end the run.";
            return "Restart Simulation 2 — work step by step and stay within each task time limit.";
        }

        if (outcome != null && outcome.timedOut)
        {
            if (stage == SimulationStage.Sim1)
                return "Retry Simulation 1 — map the route first, then collect items and reach shelter before time runs out.";
            return "Retry Simulation 2 — kit first, then contact and report, then complete treatment steps.";
        }

        if (outcome != null && outcome.WasSlow && outcome.missionCompleted && outcome.timeLimitSeconds > 0f)
        {
            if (stage == SimulationStage.Sim1)
                return "You finished, but used most of the time — practice Simulation 1 again for a smoother, faster run.";
            return "Practice Simulation 2 again to build speed without skipping safety steps.";
        }

        var peakBand = StressChangeIndexCalculator.Classify(peakSci);

        if (stage == SimulationStage.Sim1)
        {
            if (outcome != null && outcome.WasFast && outcome.timeLimitSeconds > 0f)
                return "Good pace and regulation — proceed to Simulation 2 for the outdoor first-aid stage.";
            if (peakBand == StressChangeIndexCalculator.StressBand.High)
                return "Take a short Environment Learning tour to orient yourself, then retry Simulation 1 with box breathing between tasks.";
            if (peakBand == StressChangeIndexCalculator.StressBand.Moderate)
                return "When ready, move to Simulation 2 — use the breathing tip before starting the outdoor stage.";
            return "You regulated well — proceed to Simulation 2 for a harder sustained-stress scenario.";
        }

        if (peakBand == StressChangeIndexCalculator.StressBand.High)
            return "Repeat Simulation 2 after a recovery break, or run Environment Learning to reduce cognitive load from navigation.";

        var priorSim1 = SessionHistoryStore.GetLatestPriorSim1();
        SessionHistoryStore.TryGetCurrentSession(out var current);
        if (current != null && priorSim1 == null && current.sim1PeakSci >= 50f)
            return "Simulation 1 peak was high earlier today — consider repeating it with breathing practice before another full session.";

        if (outcome != null && outcome.WasFast && outcome.timeLimitSeconds > 0f)
            return "Strong finish — schedule another full session later this week to track progress.";

        return "Schedule another full session later this week to track whether peak SCI and recovery keep improving.";
    }

    static string BuildTimeMetricsLine(SimulationRunOutcome outcome)
    {
        if (outcome == null)
            return string.Empty;

        if (outcome.disqualified)
        {
            string reason = string.IsNullOrEmpty(outcome.disqualificationReason)
                ? $"Disqualified — {outcome.taskStrikeCount} task time violations"
                : outcome.disqualificationReason;
            return $"Status: {reason}";
        }

        if (outcome.timeLimitSeconds <= 0f)
        {
            var sb = new StringBuilder();
            if (outcome.taskStrikeCount > 0)
                sb.AppendLine($"Task strikes: {outcome.taskStrikeCount}/3");
            if (outcome.elapsedSeconds > 0f)
                sb.Append($"Run time: {FormatDuration(outcome.elapsedSeconds)}");
            if (outcome.missionCompleted && outcome.taskStrikeCount == 0)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append("All tasks completed on time");
            }

            return sb.ToString().TrimEnd();
        }

        string elapsed = FormatDuration(outcome.elapsedSeconds);
        string limit = FormatDuration(outcome.timeLimitSeconds);

        if (outcome.taskStrikeCount > 0)
        {
            string strikeNote = $"Task strikes: {outcome.taskStrikeCount}/3";
            if (outcome.timedOut)
            {
                int pct = Mathf.RoundToInt(outcome.completionRatio * 100f);
                return $"{strikeNote}\nTime: {elapsed} / {limit} — not finished ({pct}% done)";
            }

            if (outcome.WasFast)
                return $"{strikeNote}\nTime: {elapsed} / {limit} — finished early";

            if (outcome.WasSlow)
                return $"{strikeNote}\nTime: {elapsed} / {limit} — slow finish";

            if (outcome.missionCompleted)
                return $"{strikeNote}\nTime: {elapsed} / {limit} — completed";

            return strikeNote;
        }

        if (outcome.timedOut)
        {
            int pct = Mathf.RoundToInt(outcome.completionRatio * 100f);
            return $"Time: {elapsed} / {limit} — not finished ({pct}% done)";
        }

        if (outcome.WasFast)
            return $"Time: {elapsed} / {limit} — finished early";

        if (outcome.WasSlow)
            return $"Time: {elapsed} / {limit} — slow finish";

        return $"Time: {elapsed} / {limit} — completed";
    }

    static string BuildTimeAdvice(SimulationRunOutcome outcome, SimulationStage stage)
    {
        if (outcome == null)
            return string.Empty;

        if (outcome.disqualified)
            return "Simulation stopped — too many task steps exceeded their time limit. Start the simulation again from the beginning.";

        if (outcome.taskStrikeCount > 0 && !outcome.disqualified)
            return $"Warning: {outcome.taskStrikeCount}/3 task time violations — move faster on the current objective.";

        if (outcome.timedOut)
        {
            int pct = Mathf.RoundToInt(outcome.completionRatio * 100f);
            return stage == SimulationStage.Sim1
                ? $"Mission not finished in time ({pct}% done) — retry with one clear task at a time."
                : "Mission not finished in time — follow kit → contact → report → treatment in order.";
        }

        if (outcome.missionCompleted && outcome.taskStrikeCount == 0)
            return "All task steps completed within their time limits — good pace.";

        if (outcome.WasFast)
            return "Strong pace — you finished with time to spare while staying focused.";

        if (outcome.WasSlow)
            return "You completed the mission, but used most of the allowed time — plan your next moves before moving.";

        return string.Empty;
    }

    static string BuildEmptyRunFeedback(
        SimulationRunOutcome outcome,
        SimulationStage stage,
        RecommendationsPanelDisplayOptions options = null)
    {
        options ??= RecommendationsPanelDisplayOptions.AllEnabled();

        if (options.showTimeAdvice && outcome != null && outcome.disqualified)
            return BuildTimeAdvice(outcome, stage);

        if (options.showTimeAdvice && outcome != null && outcome.timedOut)
            return BuildTimeAdvice(outcome, stage);

        return "Run the simulation again to get feedback.";
    }

    static string FormatDuration(float seconds)
    {
        int sec = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int m = sec / 60;
        int s = sec % 60;
        return $"{m:00}:{s:00}";
    }

    static float priorPeak(SessionHistoryStore.SessionRecord prior, SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? prior.sim1PeakSci : prior.sim2PeakSci;

    static float PeakForStage(SessionHistoryStore.SessionRecord record, SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? record.sim1PeakSci : record.sim2PeakSci;

    static string StageLabel(SimulationStage stage) =>
        stage == SimulationStage.Sim1 ? "Simulation 1" : "Simulation 2";

}
