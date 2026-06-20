using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Persists session summaries (baseline HRV, SCI stats, recommendations) to disk (JSON).
/// </summary>
public static class SessionHistoryStore
{
    private const string FileName = "stress_trainer_sessions.json";

    private static string PathFile => Path.Combine(Application.persistentDataPath, FileName);

    public static string CurrentSessionId { get; private set; }

    [Serializable]
    public class SessionRecord
    {
        public string id;
        public string userEmail;
        public string startedUtc;
        public string endedUtc;
        public float sessionDurationSeconds;
        public float baselineHrvMs;
        public float sim1MeanSci;
        public float sim1PeakSci;
        public int sim1Samples;
        public float sim1RecoverySeconds;
        public float sim1DurationSeconds;
        public float sim1TimeLimitSeconds;
        public bool sim1MissionCompleted;
        public bool sim1TimedOut;
        public float sim1MissionProgress;
        public float sim1HighStressSeconds;
        public bool sim1Disqualified;
        public int sim1TaskStrikeCount;
        public string sim1DisqualificationReason;
        public float sim2MeanSci;
        public float sim2PeakSci;
        public int sim2Samples;
        public float sim2RecoverySeconds;
        public float sim2DurationSeconds;
        public float sim2TimeLimitSeconds;
        public bool sim2MissionCompleted;
        public bool sim2TimedOut;
        public float sim2MissionProgress;
        public float sim2HighStressSeconds;
        public bool sim2Disqualified;
        public int sim2TaskStrikeCount;
        public string sim2DisqualificationReason;
        public string recommendationSim1;
        public string recommendationSim2;
    }

    [Serializable]
    private class Wrapper
    {
        public List<SessionRecord> sessions = new List<SessionRecord>();
    }

    public static string ActiveUserEmail => NormalizeUserEmail(LocalAuthStore.GetCurrentLoggedInEmail());

    public static void BeginSession(float baselineHrvMs)
    {
        CurrentSessionId = Guid.NewGuid().ToString("N");
        var rec = new SessionRecord
        {
            id = CurrentSessionId,
            userEmail = ActiveUserEmail,
            startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            baselineHrvMs = baselineHrvMs
        };
        var all = LoadAll();
        all.sessions.Add(rec);
        SaveAll(all);
    }

    public static void UpdateAfterSim1(
        IReadOnlyList<float> sciHistory,
        float baselineHrv,
        SimulationRunOutcome outcome,
        float sampleIntervalSeconds = 0.4f)
    {
        var all = LoadAll();
        var rec = all.sessions.LastOrDefault(r => r.id == CurrentSessionId);
        if (rec == null)
        {
            CurrentSessionId = Guid.NewGuid().ToString("N");
            rec = new SessionRecord
            {
                id = CurrentSessionId,
                userEmail = ActiveUserEmail,
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                baselineHrvMs = baselineHrv
            };
            all.sessions.Add(rec);
        }

        rec.userEmail = ActiveUserEmail;
        rec.baselineHrvMs = baselineHrv;
        ApplySim1Outcome(rec, outcome);

        if (sciHistory != null && sciHistory.Count > 0)
        {
            rec.sim1MeanSci = sciHistory.Average();
            rec.sim1PeakSci = sciHistory.Max();
            rec.sim1Samples = sciHistory.Count;
            rec.sim1RecoverySeconds = EstimateRecoverySeconds(sciHistory, sampleIntervalSeconds);
        }

        rec.recommendationSim1 = StressRecommendations.BuildRecommendationsTabOnly(
            sciHistory,
            StressRecommendations.SimulationStage.Sim1,
            outcome);
        SaveAll(all);
    }

    public static bool TryGetCurrentSession(out SessionRecord record)
    {
        var all = LoadAll();
        record = all.sessions.LastOrDefault(r => r.id == CurrentSessionId);
        return record != null;
    }

    public static IReadOnlyList<SessionRecord> GetPriorSessions(string userEmail = null, int maxCount = 8)
    {
        string email = NormalizeUserEmail(userEmail ?? ActiveUserEmail);
        return LoadAll().sessions
            .Where(s => s != null && s.id != CurrentSessionId && UserMatches(s, email) && HasAnySimData(s))
            .OrderByDescending(s => ParseUtc(s.startedUtc))
            .Take(maxCount)
            .ToList();
    }

    public static SessionRecord GetLatestPriorSim1(string userEmail = null)
    {
        return GetPriorSessions(userEmail).FirstOrDefault(s => s.sim1Samples > 0);
    }

    public static SessionRecord GetLatestPriorSim2(string userEmail = null)
    {
        return GetPriorSessions(userEmail).FirstOrDefault(s => s.sim2Samples > 0);
    }

    public static int CountPriorSessions(string userEmail = null)
    {
        return GetPriorSessions(userEmail, 100).Count;
    }

    /// <summary>Compares Simulation 2 SCI peaks/means to stored Simulation 1 stats for the current session.</summary>
    public static string BuildPhysiologicalRecoverySummary(float sim2PeakSci, float sim2MeanSci)
    {
        if (!TryGetCurrentSession(out var s))
            return "No session data yet — complete Simulation 1 first for comparison.";

        if (s.sim1Samples <= 0)
            return "Simulation 1 metrics missing — run the indoor scenario first to compare recovery.";

        string trend;
        if (sim2PeakSci < s.sim1PeakSci - 5f)
            trend = "Peak stress was lower than in Simulation 1 — possible faster physiological recovery or habituation under sustained load.";
        else if (sim2PeakSci > s.sim1PeakSci + 5f)
            trend = "Peak stress exceeded Simulation 1 — prolonged alarm and cognitive demand may have kept arousal elevated.";
        else
            trend = "Peak stress was similar to Simulation 1 — comparable physiological strain across both stages.";

        string recoveryLine = string.Empty;
        if (s.sim1RecoverySeconds >= 0f && s.sim2RecoverySeconds >= 0f)
        {
            if (s.sim2RecoverySeconds < s.sim1RecoverySeconds - 2f)
                recoveryLine = $"\nRecovery to low stress was faster in Simulation 2 ({s.sim2RecoverySeconds:F0}s vs {s.sim1RecoverySeconds:F0}s in Simulation 1).";
            else if (s.sim2RecoverySeconds > s.sim1RecoverySeconds + 2f)
                recoveryLine = $"\nRecovery took longer in Simulation 2 ({s.sim2RecoverySeconds:F0}s vs {s.sim1RecoverySeconds:F0}s in Simulation 1).";
        }

        return
            "Physiological profile (SCI vs Simulation 1)\n" +
            $"Simulation 1 — peak: {s.sim1PeakSci:F1}%, mean: {s.sim1MeanSci:F1}%\n" +
            $"Simulation 2 — peak: {sim2PeakSci:F1}%, mean: {sim2MeanSci:F1}%\n\n" +
            trend +
            recoveryLine;
    }

    public static void FinalizeAfterSim2(
        IReadOnlyList<float> sciHistory,
        SimulationRunOutcome outcome,
        float sampleIntervalSeconds = 0.4f)
    {
        var all = LoadAll();
        var rec = all.sessions.LastOrDefault(r => r.id == CurrentSessionId);
        if (rec == null)
        {
            CurrentSessionId = Guid.NewGuid().ToString("N");
            rec = new SessionRecord
            {
                id = CurrentSessionId,
                userEmail = ActiveUserEmail,
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
            all.sessions.Add(rec);
        }

        rec.userEmail = ActiveUserEmail;
        ApplySim2Outcome(rec, outcome);

        if (sciHistory != null && sciHistory.Count > 0)
        {
            rec.sim2MeanSci = sciHistory.Average();
            rec.sim2PeakSci = sciHistory.Max();
            rec.sim2Samples = sciHistory.Count;
            rec.sim2RecoverySeconds = EstimateRecoverySeconds(sciHistory, sampleIntervalSeconds);
        }

        rec.recommendationSim2 = StressRecommendations.BuildRecommendationsTabOnly(
            sciHistory,
            StressRecommendations.SimulationStage.Sim2,
            outcome);
        rec.endedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        if (DateTime.TryParse(rec.startedUtc, out var started))
            rec.sessionDurationSeconds = Mathf.Max(0f, (float)(DateTime.UtcNow - started).TotalSeconds);
        SaveAll(all);
    }

    static void ApplySim1Outcome(SessionRecord rec, SimulationRunOutcome outcome)
    {
        if (outcome == null)
            return;

        rec.sim1DurationSeconds = outcome.elapsedSeconds;
        rec.sim1TimeLimitSeconds = outcome.timeLimitSeconds;
        rec.sim1MissionCompleted = outcome.missionCompleted;
        rec.sim1TimedOut = outcome.timedOut;
        rec.sim1MissionProgress = outcome.completionRatio;
        rec.sim1HighStressSeconds = outcome.highStressSeconds;
        rec.sim1Disqualified = outcome.disqualified;
        rec.sim1TaskStrikeCount = outcome.taskStrikeCount;
        rec.sim1DisqualificationReason = outcome.disqualificationReason ?? string.Empty;
    }

    static void ApplySim2Outcome(SessionRecord rec, SimulationRunOutcome outcome)
    {
        if (outcome == null)
            return;

        rec.sim2DurationSeconds = outcome.elapsedSeconds;
        rec.sim2TimeLimitSeconds = outcome.timeLimitSeconds;
        rec.sim2MissionCompleted = outcome.missionCompleted;
        rec.sim2TimedOut = outcome.timedOut;
        rec.sim2MissionProgress = outcome.completionRatio;
        rec.sim2HighStressSeconds = outcome.highStressSeconds;
        rec.sim2Disqualified = outcome.disqualified;
        rec.sim2TaskStrikeCount = outcome.taskStrikeCount;
        rec.sim2DisqualificationReason = outcome.disqualificationReason ?? string.Empty;
    }

    public static SimulationRunOutcome BuildOutcomeFromRecord(
        SessionRecord record,
        StressRecommendations.SimulationStage stage)
    {
        if (record == null)
            return null;

        if (stage == StressRecommendations.SimulationStage.Sim1 &&
            record.sim1Samples <= 0 && record.sim1DurationSeconds <= 0f)
            return null;

        if (stage == StressRecommendations.SimulationStage.Sim2 &&
            record.sim2Samples <= 0 && record.sim2DurationSeconds <= 0f)
            return null;

        if (stage == StressRecommendations.SimulationStage.Sim1)
        {
            return SimulationRunOutcome.Create(
                record.sim1DurationSeconds,
                record.sim1TimeLimitSeconds > 0f ? record.sim1TimeLimitSeconds : 300f,
                record.sim1MissionCompleted,
                record.sim1TimedOut,
                record.sim1MissionProgress,
                record.sim1HighStressSeconds,
                record.sim1Disqualified,
                record.sim1TaskStrikeCount,
                record.sim1DisqualificationReason);
        }

        return SimulationRunOutcome.Create(
            record.sim2DurationSeconds,
            record.sim2TimeLimitSeconds > 0f ? record.sim2TimeLimitSeconds : 600f,
            record.sim2MissionCompleted,
            record.sim2TimedOut,
            record.sim2MissionProgress,
            record.sim2HighStressSeconds,
            record.sim2Disqualified,
            record.sim2TaskStrikeCount,
            record.sim2DisqualificationReason);
    }

    private static bool HasAnySimData(SessionRecord record) =>
        record.sim1Samples > 0 || record.sim2Samples > 0;

    private static bool UserMatches(SessionRecord record, string userEmail)
    {
        string stored = NormalizeUserEmail(record.userEmail);
        return stored == userEmail;
    }

    private static string NormalizeUserEmail(string email) =>
        string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();

    private static DateTime ParseUtc(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return parsed;
        return DateTime.MinValue;
    }

    private static float EstimateRecoverySeconds(IReadOnlyList<float> sciHistory, float sampleIntervalSeconds)
    {
        if (sciHistory == null || sciHistory.Count == 0 || sampleIntervalSeconds <= 0f)
            return -1f;

        int peakIndex = 0;
        float peak = sciHistory[0];
        for (int i = 1; i < sciHistory.Count; i++)
        {
            if (sciHistory[i] > peak)
            {
                peak = sciHistory[i];
                peakIndex = i;
            }
        }

        for (int i = peakIndex; i < sciHistory.Count; i++)
        {
            if (sciHistory[i] <= 20f)
                return (i - peakIndex) * sampleIntervalSeconds;
        }

        return -1f;
    }

    private static Wrapper LoadAll()
    {
        try
        {
            if (!File.Exists(PathFile)) return new Wrapper();
            string json = File.ReadAllText(PathFile, Encoding.UTF8);
            return JsonUtility.FromJson<Wrapper>(json) ?? new Wrapper();
        }
        catch
        {
            return new Wrapper();
        }
    }

    private static void SaveAll(Wrapper w)
    {
        try
        {
            string json = JsonUtility.ToJson(w, true);
            File.WriteAllText(PathFile, json, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SessionHistoryStore save failed: {e.Message}");
        }
    }
}
