using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HR/HRV for development (synthetic) and optional live samples from <see cref="UDPReceiver"/> (Android gateway).
/// </summary>
public class MockPhysiologySource : MonoBehaviour
{
    [Header("Gateway (optional)")]
    public UDPReceiver udpReceiver;

    [Tooltip("Use packets from the phone/watch when fresh; otherwise synthetic signal.")]
    public bool useLiveUdpWhenAvailable = false;

    [Tooltip("Seconds without UDP before synthetic model is used again (when gateway is expected).")]
    public float liveDataStaleSeconds = 2f;

    [Header("Rest model (before baseline is locked)")]
    [Tooltip("Typical resting HRV scale (ms-like proxy for RMSSD).")]
    public float nominalRestHrvMs = 52f;

    public float nominalRestHeartRate = 72f;

    [Header("Stress response (while stressor active)")]
    [Range(0f, 0.95f)]
    public float maxHrvDropFraction = 0.55f;

    public float heartRateSensitivity = 0.65f;

    /// <summary>When true (siren, time pressure), HRV trends down in synthetic mode.</summary>
    public bool StressorActive { get; set; }

    /// <summary>Locked at end of baseline window (average of samples).</summary>
    public float HrvBaselineMs { get; private set; }

    public bool BaselineLocked { get; private set; }

    public float CurrentHrvMs { get; private set; }

    public float CurrentHeartRate { get; private set; }

    public bool UsingLiveUdpSample { get; private set; }

    private readonly List<float> _baselineSamples = new List<float>(600);
    private bool _capturingBaseline;

    public void StartBaselineCapture()
    {
        _capturingBaseline = true;
        BaselineLocked = false;
        _baselineSamples.Clear();
        StressorActive = false;
    }

    public void StopBaselineCaptureAndLock()
    {
        _capturingBaseline = false;
        if (_baselineSamples.Count == 0)
        {
            HrvBaselineMs = nominalRestHrvMs;
        }
        else
        {
            float sum = 0f;
            for (int i = 0; i < _baselineSamples.Count; i++)
                sum += _baselineSamples[i];
            HrvBaselineMs = sum / _baselineSamples.Count;
        }

        BaselineLocked = true;
        CurrentHrvMs = HrvBaselineMs;
    }

    private bool TryApplyLiveSample()
    {
        UsingLiveUdpSample = false;
        if (!useLiveUdpWhenAvailable || udpReceiver == null)
            return false;

        if (udpReceiver.SecondsSinceLastPacket > liveDataStaleSeconds)
            return false;

        bool haveHr = udpReceiver.heartRate > 0;
        bool haveHrv = udpReceiver.hrvMs > 0.01f;
        if (!haveHr && !haveHrv)
            return false;

        UsingLiveUdpSample = true;
        if (haveHr)
            CurrentHeartRate = udpReceiver.heartRate;

        if (haveHrv)
        {
            CurrentHrvMs = Mathf.Max(5f, udpReceiver.hrvMs);
        }
        else if (haveHr)
        {
            float drop = Mathf.Clamp01((udpReceiver.heartRate - nominalRestHeartRate) / 40f);
            CurrentHrvMs = Mathf.Max(5f, nominalRestHrvMs * (1f - drop * 0.6f));
        }

        if (!haveHr && haveHrv)
            CurrentHeartRate = nominalRestHeartRate;

        return true;
    }

    void Update()
    {
        if (TryApplyLiveSample())
        {
            if (_capturingBaseline)
                _baselineSamples.Add(CurrentHrvMs);
            return;
        }

        float t = Time.time;
        float n = (Mathf.PerlinNoise(t * 0.22f, 0.13f) - 0.5f) * 2f;

        if (_capturingBaseline)
        {
            float rest = nominalRestHrvMs + n * 3f;
            CurrentHrvMs = Mathf.Max(5f, rest);
            _baselineSamples.Add(CurrentHrvMs);
        }
        else if (BaselineLocked)
        {
            float target;
            if (StressorActive)
            {
                float floor = HrvBaselineMs * (1f - maxHrvDropFraction);
                target = Mathf.Lerp(HrvBaselineMs, floor, 0.85f) + n * 4f;
            }
            else
            {
                target = Mathf.Lerp(CurrentHrvMs, HrvBaselineMs, 0.08f) + n * 2f;
            }

            CurrentHrvMs = Mathf.Max(5f, target);
        }
        else
        {
            CurrentHrvMs = Mathf.Max(5f, nominalRestHrvMs + n * 3f);
        }

        float hrvDrop = BaselineLocked ? Mathf.Max(0f, HrvBaselineMs - CurrentHrvMs) : 0f;
        float hrBump = StressorActive ? 12f : 0f;
        CurrentHeartRate = nominalRestHeartRate + hrvDrop * heartRateSensitivity + hrBump * 0.25f + n * 2f;
    }
}
