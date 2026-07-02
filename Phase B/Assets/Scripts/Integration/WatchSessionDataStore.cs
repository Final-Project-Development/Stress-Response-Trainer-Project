using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Always-on watch timeline storage. Keeps receiving UDP watch packets even when the
/// on-screen heart-rate chart is hidden or disabled.
/// </summary>
public sealed class WatchSessionDataStore : MonoBehaviour
{
    public static WatchSessionDataStore Instance { get; private set; }

    [Tooltip("After this many seconds without packets, an open session with samples is treated as complete.")]
    public float sessionIdleCompleteSeconds = 8f;

    public float LastPacketRealtime { get; private set; }
    public float LastSessionCompletedRealtime { get; private set; }
    public int TotalHeartRateSamplesReceived { get; private set; }

    public event Action<string> OnWatchSessionCompleted;

    private UDPReceiver _udpReceiver;
    private string _currentSessionId;
    private string _lastCompletedSessionId;
    private readonly Dictionary<string, HrSessionData> _sessions = new Dictionary<string, HrSessionData>();

    private class HrSessionData
    {
        public string sessionId;
        public int expectedSampleCount;
        public readonly List<HrSample> samples = new List<HrSample>();
        public readonly HashSet<string> sampleKeys = new HashSet<string>();
    }

    private struct HrSample
    {
        public float bpm;
        public float receivedRealtime;
    }

    public void Configure(UDPReceiver udpReceiver)
    {
        if (_udpReceiver == udpReceiver)
            return;

        Unsubscribe();
        _udpReceiver = udpReceiver;
        Subscribe();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        TryCompleteIdleSession();
    }

    private void Subscribe()
    {
        if (_udpReceiver == null)
            return;

        _udpReceiver.OnSampleReceived += HandleWatchSample;
    }

    private void Unsubscribe()
    {
        if (_udpReceiver == null)
            return;

        _udpReceiver.OnSampleReceived -= HandleWatchSample;
        _udpReceiver = null;
    }

    private void HandleWatchSample(UDPReceiver.WatchSample sample)
    {
        if (sample == null)
            return;

        LastPacketRealtime = Time.realtimeSinceStartup;

        if (sample.IsSessionStart)
        {
            string sessionId = string.IsNullOrWhiteSpace(sample.sessionId) ? "watch-session" : sample.sessionId;
            _currentSessionId = sessionId;
            HrSessionData session = GetOrCreateSession(sessionId);
            session.expectedSampleCount = sample.sampleCount;
            session.samples.Clear();
            session.sampleKeys.Clear();
            Debug.Log($"WatchSessionDataStore: session started ({sessionId}, expected {sample.sampleCount})");
            return;
        }

        if (sample.IsSessionEnd)
        {
            string sessionId = string.IsNullOrWhiteSpace(sample.sessionId) ? _currentSessionId : sample.sessionId;
            if (!string.IsNullOrWhiteSpace(sessionId))
                CompleteSession(sessionId);
            return;
        }

        if (!sample.IsHeartRate)
            return;

        float bpm = sample.ResolvedHeartRateBpm;
        if (bpm <= 0f)
            return;

        string hrSessionId = string.IsNullOrWhiteSpace(sample.sessionId)
            ? BuildLiveSessionId(sample.source, sample.device)
            : sample.sessionId;

        AppendHeartRateSample(hrSessionId, bpm);
    }

    private void AppendHeartRateSample(string sessionId, float bpm)
    {
        HrSessionData session = GetOrCreateSession(sessionId);
        string key = $"{TotalHeartRateSamplesReceived}|{bpm:0.0}";
        if (session.sampleKeys.Contains(key))
            return;

        session.sampleKeys.Add(key);
        session.samples.Add(new HrSample
        {
            bpm = bpm,
            receivedRealtime = Time.realtimeSinceStartup
        });

        _currentSessionId = sessionId;
        TotalHeartRateSamplesReceived++;
    }

    private void TryCompleteIdleSession()
    {
        if (string.IsNullOrWhiteSpace(_currentSessionId))
            return;

        if (LastPacketRealtime <= 0f ||
            Time.realtimeSinceStartup - LastPacketRealtime < sessionIdleCompleteSeconds)
        {
            return;
        }

        if (!_sessions.TryGetValue(_currentSessionId, out HrSessionData session) || session.samples.Count == 0)
            return;

        CompleteSession(_currentSessionId);
    }

    private void CompleteSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) ||
            !_sessions.TryGetValue(sessionId, out HrSessionData session) ||
            session.samples.Count == 0)
        {
            return;
        }

        _lastCompletedSessionId = sessionId;
        LastSessionCompletedRealtime = Time.realtimeSinceStartup;
        OnWatchSessionCompleted?.Invoke(sessionId);
        Debug.Log($"WatchSessionDataStore: session completed ({sessionId}, {session.samples.Count} samples)");
    }

    public bool TryGetLatestCompletedSessionHeartRates(out List<float> heartRatesBpm)
    {
        heartRatesBpm = null;
        if (string.IsNullOrWhiteSpace(_lastCompletedSessionId) ||
            !_sessions.TryGetValue(_lastCompletedSessionId, out HrSessionData session) ||
            session.samples.Count == 0)
        {
            return false;
        }

        heartRatesBpm = session.samples.Select(s => s.bpm).ToList();
        return heartRatesBpm.Count > 0;
    }

    /// <summary>
    /// Heart-rate samples from the best available session for results (completed session,
    /// or an idle open session that received data after <paramref name="simulationEndRealtime"/>.
    /// </summary>
    public bool TryGetHeartRatesForResults(float simulationEndRealtime, out List<float> heartRatesBpm)
    {
        heartRatesBpm = null;

        if (LastSessionCompletedRealtime >= simulationEndRealtime &&
            TryGetLatestCompletedSessionHeartRates(out heartRatesBpm))
        {
            return true;
        }

        HrSessionData bestSession = null;
        float bestScore = float.MinValue;

        foreach (KeyValuePair<string, HrSessionData> pair in _sessions)
        {
            HrSessionData session = pair.Value;
            if (session.samples.Count == 0)
                continue;

            int afterEndCount = 0;
            float latestAfterEnd = float.MinValue;
            for (int i = 0; i < session.samples.Count; i++)
            {
                if (session.samples[i].receivedRealtime + 0.05f >= simulationEndRealtime)
                {
                    afterEndCount++;
                    latestAfterEnd = Mathf.Max(latestAfterEnd, session.samples[i].receivedRealtime);
                }
            }

            if (afterEndCount == 0)
                continue;

            float score = afterEndCount * 1000f + latestAfterEnd;
            if (score > bestScore)
            {
                bestScore = score;
                bestSession = session;
            }
        }

        if (bestSession == null)
            return false;

        bool idleEnough = LastPacketRealtime <= 0f ||
                          Time.realtimeSinceStartup - LastPacketRealtime >= sessionIdleCompleteSeconds;
        if (!idleEnough && LastSessionCompletedRealtime < simulationEndRealtime)
            return false;

        heartRatesBpm = bestSession.samples
            .Where(s => s.receivedRealtime + 0.05f >= simulationEndRealtime)
            .Select(s => s.bpm)
            .ToList();

        if (heartRatesBpm.Count == 0)
            heartRatesBpm = bestSession.samples.Select(s => s.bpm).ToList();

        return heartRatesBpm.Count > 0;
    }

    public bool HasUsableResultsData(float simulationEndRealtime)
    {
        if (TryGetHeartRatesForResults(simulationEndRealtime, out _))
            return true;

        return LastSessionCompletedRealtime >= simulationEndRealtime;
    }

    private HrSessionData GetOrCreateSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out HrSessionData session))
        {
            session = new HrSessionData { sessionId = sessionId };
            _sessions[sessionId] = session;
        }

        return session;
    }

    private static string BuildLiveSessionId(string source, string device)
    {
        string id = !string.IsNullOrWhiteSpace(source) ? source : device;
        if (string.IsNullOrWhiteSpace(id))
            id = "watch";

        id = id.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
        return $"live-{id}";
    }
}
