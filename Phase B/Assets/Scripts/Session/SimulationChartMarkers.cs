using UnityEngine;

/// <summary>Records mission milestone markers on the active simulation SCI chart.</summary>
public static class SimulationChartMarkers
{
    public static void Record(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var flow = Object.FindFirstObjectByType<TrainingFlowController>(FindObjectsInactive.Include);
        if (flow != null && flow.recorder != null)
        {
            flow.recorder.RecordMissionMarker(label.Trim());
            return;
        }

        var recorder = Object.FindFirstObjectByType<SessionStressRecorder>(FindObjectsInactive.Include);
        recorder?.RecordMissionMarker(label.Trim());
    }
}
