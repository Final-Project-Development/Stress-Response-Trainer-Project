using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Samples SCI during an active simulation for a retrospective line graph.
/// </summary>
public class SessionStressRecorder : MonoBehaviour
{
    public float sampleIntervalSeconds = 0.4f;

    private float _timer;
    private bool _recording;

    public IReadOnlyList<float> SciHistory => _sci;
    public IReadOnlyList<float> HrvHistory => _hrv;
    private readonly List<float> _sci = new List<float>(256);
    private readonly List<float> _hrv = new List<float>(256);

    public void BeginRecording()
    {
        _sci.Clear();
        _hrv.Clear();
        _timer = 0f;
        _recording = true;
    }

    public void EndRecording()
    {
        _recording = false;
    }

    public void Clear()
    {
        _sci.Clear();
        _hrv.Clear();
        _recording = false;
        _timer = 0f;
    }

    void Update()
    {
        if (!_recording) return;

        _timer += Time.deltaTime;
        if (_timer < sampleIntervalSeconds) return;

        _timer = 0f;
    }

    /// <summary>Call from flow controller when you have fresh SCI each tick, or use TickRecord.</summary>
    public void RecordSample(float sciPercent)
    {
        if (!_recording) return;
        _sci.Add(sciPercent);
    }

    public void TickRecord(float sciPercent, float hrvMs = -1f)
    {
        if (!_recording) return;
        _timer += Time.deltaTime;
        if (_timer < sampleIntervalSeconds) return;
        _timer = 0f;
        _sci.Add(sciPercent);
        if (hrvMs > 0f)
            _hrv.Add(hrvMs);
    }
}
