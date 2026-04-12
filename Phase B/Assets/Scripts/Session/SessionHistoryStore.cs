using System;
using System.Collections.Generic;
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
        public string startedUtc;
        public string endedUtc;
        public float sessionDurationSeconds;
        public float baselineHrvMs;
        public float sim1MeanSci;
        public float sim1PeakSci;
        public int sim1Samples;
        public float sim1RecoverySeconds;
        public float sim2MeanSci;
        public float sim2PeakSci;
        public int sim2Samples;
        public float sim2RecoverySeconds;
        public string recommendationSim1;
        public string recommendationSim2;
    }

    [Serializable]
    private class Wrapper
    {
        public List<SessionRecord> sessions = new List<SessionRecord>();
    }

    public static void BeginSession(float baselineHrvMs)
    {
        CurrentSessionId = Guid.NewGuid().ToString("N");
        var rec = new SessionRecord
        {
            id = CurrentSessionId,
            startedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            baselineHrvMs = baselineHrvMs
        };
        var all = LoadAll();
        all.sessions.Add(rec);
        SaveAll(all);
    }

    public static void UpdateAfterSim1(IReadOnlyList<float> sciHistory, float baselineHrv, float sampleIntervalSeconds = 0.4f)
    {
        var all = LoadAll();
        var rec = all.sessions.LastOrDefault(r => r.id == CurrentSessionId);
        if (rec == null)
        {
            CurrentSessionId = Guid.NewGuid().ToString("N");
            rec = new SessionRecord
            {
                id = CurrentSessionId,
                startedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                baselineHrvMs = baselineHrv
            };
            all.sessions.Add(rec);
        }

        rec.baselineHrvMs = baselineHrv;
        if (sciHistory != null && sciHistory.Count > 0)
        {
            rec.sim1MeanSci = sciHistory.Average();
            rec.sim1PeakSci = sciHistory.Max();
            rec.sim1Samples = sciHistory.Count;
            rec.sim1RecoverySeconds = EstimateRecoverySeconds(sciHistory, sampleIntervalSeconds);
        }

        rec.recommendationSim1 = StressRecommendations.BuildFromSciHistory(sciHistory);
        SaveAll(all);
    }

    public static bool TryGetCurrentSession(out SessionRecord record)
    {
        var all = LoadAll();
        record = all.sessions.LastOrDefault(r => r.id == CurrentSessionId);
        return record != null;
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

        return
            "Physiological profile (SCI vs Simulation 1)\n" +
            $"Simulation 1 — peak: {s.sim1PeakSci:F1}%, mean: {s.sim1MeanSci:F1}%\n" +
            $"Simulation 2 — peak: {sim2PeakSci:F1}%, mean: {sim2MeanSci:F1}%\n\n" +
            trend;
    }

    public static void FinalizeAfterSim2(IReadOnlyList<float> sciHistory, float sampleIntervalSeconds = 0.4f)
    {
        var all = LoadAll();
        var rec = all.sessions.LastOrDefault(r => r.id == CurrentSessionId);
        if (rec == null)
        {
            CurrentSessionId = Guid.NewGuid().ToString("N");
            rec = new SessionRecord { id = CurrentSessionId, startedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture) };
            all.sessions.Add(rec);
        }

        if (sciHistory != null && sciHistory.Count > 0)
        {
            rec.sim2MeanSci = sciHistory.Average();
            rec.sim2PeakSci = sciHistory.Max();
            rec.sim2Samples = sciHistory.Count;
            rec.sim2RecoverySeconds = EstimateRecoverySeconds(sciHistory, sampleIntervalSeconds);
        }

        rec.recommendationSim2 = StressRecommendations.BuildFromSciHistory(sciHistory);
        rec.endedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        if (DateTime.TryParse(rec.startedUtc, out var started))
            rec.sessionDurationSeconds = Mathf.Max(0f, (float)(DateTime.UtcNow - started).TotalSeconds);
        SaveAll(all);
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

        // Recovery criterion: back to low-stress zone (SCI <= 20%).
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
