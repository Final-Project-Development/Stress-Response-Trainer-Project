using UnityEngine;

/// <summary>
/// Mission timing + completion context for results, history, and recommendations.
/// </summary>
public class SimulationRunOutcome
{
    public float elapsedSeconds;
    public float timeLimitSeconds;
    public bool missionCompleted;
    public bool timedOut;
    public bool disqualified;
    public int taskStrikeCount;
    public string disqualificationReason;
    /// <summary>0–1 mission step progress when the run ended.</summary>
    public float completionRatio;
    /// <summary>Seconds spent at or above high-stress SCI threshold during the run.</summary>
    public float highStressSeconds;

    public bool WasFast =>
        missionCompleted &&
        timeLimitSeconds > 0f &&
        elapsedSeconds <= timeLimitSeconds * 0.5f;

    public bool WasSlow =>
        missionCompleted &&
        timeLimitSeconds > 0f &&
        elapsedSeconds >= timeLimitSeconds * 0.85f;

    public static SimulationRunOutcome Create(
        float elapsedSeconds,
        float timeLimitSeconds,
        bool missionCompleted,
        bool timedOut,
        float completionRatio,
        float highStressSeconds = 0f,
        bool disqualified = false,
        int taskStrikeCount = 0,
        string disqualificationReason = null)
    {
        return new SimulationRunOutcome
        {
            elapsedSeconds = Mathf.Max(0f, elapsedSeconds),
            timeLimitSeconds = Mathf.Max(0f, timeLimitSeconds),
            missionCompleted = missionCompleted,
            timedOut = timedOut,
            disqualified = disqualified,
            taskStrikeCount = Mathf.Max(0, taskStrikeCount),
            disqualificationReason = string.IsNullOrWhiteSpace(disqualificationReason)
                ? string.Empty
                : disqualificationReason.Trim(),
            completionRatio = Mathf.Clamp01(completionRatio),
            highStressSeconds = Mathf.Max(0f, highStressSeconds)
        };
    }
}
