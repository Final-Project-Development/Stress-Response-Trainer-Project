using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Listens for UDP packets from the Android gateway (Wi‑Fi). Supports HR-only or HR+HRV payloads.
/// Expected text formats: "HR:75" | "HR:75,HRV:52.3" | compact JSON {"hr":75,"hrv":52.3}
/// </summary>
public class UDPReceiver : MonoBehaviour
{
    public int listenPort = 5005;

    /// <summary>When true, consumers may show a disconnect warning if no packet arrives within the stale window.</summary>
    public bool expectGatewayTraffic;

    [Header("Live Data")]
    public int heartRate;

    /// <summary>HRV proxy in milliseconds (e.g. RMSSD-like), when provided by the gateway.</summary>
    public float hrvMs;

    [Header("Debug")]
    public string status = "Not started";
    public string lastPacket = "(none)";
    public int packetsReceived;

    /// <summary>Unity unscaled time of the last successfully parsed packet.</summary>
    public float LastReceiveRealtime { get; private set; }

    public float SecondsSinceLastPacket =>
        LastReceiveRealtime <= 0f ? 999f : Time.realtimeSinceStartup - LastReceiveRealtime;

    public bool ReceivedAnyPacket => LastReceiveRealtime > 0f;

    private UdpClient _client;
    private IPEndPoint _remoteEndPoint;

    void Start()
    {
        try
        {
            _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _client = new UdpClient(listenPort);
            status = $"Listening UDP on port {listenPort}";
            Debug.Log(status);
        }
        catch (Exception e)
        {
            status = $"UDP start ERROR: {e.Message}";
            Debug.LogError(status);
        }
    }

    void Update()
    {
        if (_client == null) return;

        try
        {
            while (_client.Available > 0)
            {
                byte[] data = _client.Receive(ref _remoteEndPoint);
                string msg = Encoding.UTF8.GetString(data).Trim();
                lastPacket = $"{_remoteEndPoint.Address}:{_remoteEndPoint.Port} -> {msg}";
                ApplyPayload(msg);
            }
        }
        catch (Exception e)
        {
            status = $"UDP receive ERROR: {e.Message}";
            Debug.LogError(status);
        }
    }

    private void ApplyPayload(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;

        bool any = false;
        msg = msg.Trim();

        if (msg.StartsWith("{", StringComparison.Ordinal))
        {
            any = TryParseJsonLoose(msg, out int hr, out float hrv);
            if (any)
            {
                if (hr > 0) heartRate = hr;
                if (hrv > 0.01f) hrvMs = hrv;
            }
        }
        else
        {
            foreach (string part in msg.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int c = part.IndexOf(':');
                if (c <= 0) continue;
                string key = part.Substring(0, c).Trim();
                string val = part.Substring(c + 1).Trim();
                if (key.Equals("HR", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hr))
                {
                    heartRate = hr;
                    any = true;
                }
                else if (key.Equals("HRV", StringComparison.OrdinalIgnoreCase) &&
                         float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float hrv))
                {
                    hrvMs = Mathf.Max(0f, hrv);
                    any = true;
                }
            }
        }

        if (any)
        {
            LastReceiveRealtime = Time.realtimeSinceStartup;
            packetsReceived++;
        }
    }

    private static bool TryParseJsonLoose(string msg, out int hr, out float hrv)
    {
        hr = 0;
        hrv = 0f;
        bool ok = false;
        int i = msg.IndexOf("\"hr\"", StringComparison.OrdinalIgnoreCase);
        if (i >= 0 && TryNumberAfterColon(msg, i, out int v))
        {
            hr = v;
            ok = true;
        }

        i = msg.IndexOf("\"hrv\"", StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            i = msg.IndexOf("\"HRV\"", StringComparison.Ordinal);
        if (i >= 0 && TryFloatAfterColon(msg, i, out float f))
        {
            hrv = f;
            ok = true;
        }

        return ok;
    }

    private static bool TryNumberAfterColon(string s, int keyIndex, out int value)
    {
        value = 0;
        int colon = s.IndexOf(':', keyIndex);
        if (colon < 0) return false;
        int start = colon + 1;
        while (start < s.Length && (s[start] == ' ' || s[start] == '"')) start++;
        int end = start;
        while (end < s.Length && char.IsDigit(s[end])) end++;
        if (end == start) return false;
        return int.TryParse(s.Substring(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryFloatAfterColon(string s, int keyIndex, out float value)
    {
        value = 0f;
        int colon = s.IndexOf(':', keyIndex);
        if (colon < 0) return false;
        int start = colon + 1;
        while (start < s.Length && (s[start] == ' ' || s[start] == '"')) start++;
        int end = start;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == '-' || s[end] == 'E' || s[end] == 'e')) end++;
        if (end == start) return false;
        return float.TryParse(s.Substring(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    void OnDestroy()
    {
        try { _client?.Close(); } catch { /* ignore */ }
        _client = null;
    }
}
