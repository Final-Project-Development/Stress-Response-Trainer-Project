using System;
using UnityEngine;

/// <summary>
/// Per-simulation toggles for the Results and Recommendations tabs.
/// Wire on <see cref="TrainingFlowController"/> — enable only the lines you want designers to see.
/// </summary>
[Serializable]
public class ResultsPanelDisplayOptions
{
    [Tooltip("Run time, task strikes, completion status, disqualification.")]
    public bool showRunTimeAndStatus = true;

    [Tooltip("Mission pace compared to the player's last run of this simulation.")]
    public bool showMissionPaceComparison = true;

    [Tooltip("Seconds spent at or above high-stress SCI (≥ 50%).")]
    public bool showHighStressTime = true;

    public bool showPeakSci = true;
    public bool showAvgSci = true;

    [Tooltip("Simulation 1 — baseline HRV from calibration.")]
    public bool showBaselineHrv = true;

    [Tooltip("Simulation 2 — min / max / average HRV during the run.")]
    public bool showHrvRange = true;

    public static ResultsPanelDisplayOptions AllEnabled() => new ResultsPanelDisplayOptions();
}

[Serializable]
public class RecommendationsPanelDisplayOptions
{
    [Tooltip("Time-limit and task-pace advice for this run.")]
    public bool showTimeAdvice = true;

    [Tooltip("Short progress hint vs previous runs (pace / stress trend).")]
    public bool showProgressHint = true;

    [Tooltip("Behavioral coping tip based on peak SCI band.")]
    public bool showBehavioralTip = true;

    [Tooltip("Suggested next step (retry, next simulation, environment learning, etc.).")]
    public bool showNextStep = true;

    [Tooltip("Simulation 2 only — brief comparison to Simulation 1.")]
    public bool showSessionComparison = true;

    public static RecommendationsPanelDisplayOptions AllEnabled() => new RecommendationsPanelDisplayOptions();
}

[Serializable]
public class SimulationResultsPanelsConfig
{
    public ResultsPanelDisplayOptions results = new ResultsPanelDisplayOptions();
    public RecommendationsPanelDisplayOptions recommendations = new RecommendationsPanelDisplayOptions();

    public static SimulationResultsPanelsConfig DefaultSim1() => new SimulationResultsPanelsConfig
    {
        results = new ResultsPanelDisplayOptions
        {
            showHrvRange = false
        },
        recommendations = new RecommendationsPanelDisplayOptions
        {
            showSessionComparison = false
        }
    };

    public static SimulationResultsPanelsConfig DefaultSim2() => new SimulationResultsPanelsConfig
    {
        results = new ResultsPanelDisplayOptions
        {
            showBaselineHrv = false
        }
    };
}
