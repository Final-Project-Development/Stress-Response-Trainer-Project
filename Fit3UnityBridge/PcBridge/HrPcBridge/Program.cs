using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var config = BridgeConfig.FromArgs(args);
Directory.CreateDirectory(config.LogDirectory);
string logPath = Path.Combine(config.LogDirectory, "hr_log.jsonl");

using var unitySender = new UdpClient();
var unityEndPoint = new IPEndPoint(IPAddress.Parse(config.UnityIp), config.UnityPort);

var logLock = new SemaphoreSlim(1, 1);
var unitySendLock = new SemaphoreSlim(1, 1);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("Fit3 / Health Connect HR PC Bridge");
Console.WriteLine("----------------------------------");
Console.WriteLine($"UDP listen port: {config.BridgePort}  [Health Connect live packets]");
Console.WriteLine($"TCP listen port: {config.BridgePort}  [Samsung Health timeline packets]");
Console.WriteLine($"Forwarding normalized packets to Unity UDP: {config.UnityIp}:{config.UnityPort}");
Console.WriteLine($"Logging to: {logPath}");
Console.WriteLine();
Console.WriteLine("Arguments/env:");
Console.WriteLine("  --bridge-port 7777        or FIT3_BRIDGE_PORT");
Console.WriteLine("  --unity-ip 127.0.0.1      or UNITY_IP");
Console.WriteLine("  --unity-port 5055         or UNITY_PORT");
Console.WriteLine("  --log-dir C:\\Fit3UnityBridge\\Logs or FIT3_LOG_DIR");
Console.WriteLine();

await Task.WhenAll(
    RunUdpListenerAsync(cts.Token),
    RunTcpListenerAsync(cts.Token)
);

async Task RunUdpListenerAsync(CancellationToken cancellationToken)
{
    using var udpReceiver = new UdpClient(config.BridgePort);
    Console.WriteLine($"UDP listener started on port {config.BridgePort}");

    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var result = await udpReceiver.ReceiveAsync(cancellationToken);
            string payload = Encoding.UTF8.GetString(result.Buffer).Trim();
            await ProcessPayloadAsync(payload, "udp", result.RemoteEndPoint.ToString(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}

async Task RunTcpListenerAsync(CancellationToken cancellationToken)
{
    var tcpListener = new TcpListener(IPAddress.Any, config.BridgePort);
    tcpListener.Start();
    Console.WriteLine($"TCP listener started on port {config.BridgePort}");

    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
            _ = Task.Run(() => HandleTcpClientAsync(client, cancellationToken), cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
        // Normal shutdown.
    }
    finally
    {
        tcpListener.Stop();
    }
}

async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
{
    await using NetworkStream stream = client.GetStream();
    using (client)
    using (var reader = new StreamReader(stream, Encoding.UTF8))
    {
        string remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            line = line.Trim();
            if (line.Length == 0)
                continue;

            await ProcessPayloadAsync(line, "tcp", remote, cancellationToken);
        }
    }
}

async Task ProcessPayloadAsync(string payload, string transport, string remote, CancellationToken cancellationToken)
{
    string pcReceivedAt = DateTimeOffset.UtcNow.ToString("O");
    string normalizedPayload = NormalizePayload(payload, pcReceivedAt, transport, remote);

    var logObject = new JsonObject
    {
        ["pcReceivedAt"] = pcReceivedAt,
        ["transport"] = transport,
        ["remote"] = remote,
        ["forwardedTo"] = $"{config.UnityIp}:{config.UnityPort}",
        ["payload"] = TryParseJsonNode(payload) ?? payload,
        ["normalized"] = TryParseJsonNode(normalizedPayload) ?? normalizedPayload
    };

    string logLine = logObject.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    Console.WriteLine(logLine);

    await logLock.WaitAsync(cancellationToken);
    try
    {
        await File.AppendAllTextAsync(logPath, logLine + Environment.NewLine, cancellationToken);
    }
    finally
    {
        logLock.Release();
    }

    byte[] bytes = Encoding.UTF8.GetBytes(normalizedPayload);

    await unitySendLock.WaitAsync(cancellationToken);
    try
    {
        await unitySender.SendAsync(bytes, bytes.Length, unityEndPoint);
    }
    finally
    {
        unitySendLock.Release();
    }
}

static string NormalizePayload(string payload, string pcReceivedAt, string transport, string remote)
{
    JsonNode? node = TryParseJsonNode(payload);
    if (node is not JsonObject obj)
        return payload;

    obj["pcReceivedAt"] = pcReceivedAt;
    obj["transport"] = transport;
    obj["remote"] = remote;
    obj["bridge"] = "HrPcBridge";

    string type = ReadString(obj, "type");
    if (string.IsNullOrWhiteSpace(type) && (TryReadFloat(obj, "bpm", out _) || TryReadFloat(obj, "hr", out _)))
    {
        type = "hr";
        obj["type"] = type;
    }

    if (string.Equals(type, "hr", StringComparison.OrdinalIgnoreCase))
    {
        float bpm = 0f;
        if (!TryReadFloat(obj, "bpm", out bpm))
            TryReadFloat(obj, "hr", out bpm);

        if (bpm > 0f)
        {
            obj["bpm"] = bpm;
            obj["hr"] = bpm;
        }

        if (string.IsNullOrWhiteSpace(ReadString(obj, "measuredAt")))
            obj["measuredAt"] = pcReceivedAt;

        if (string.IsNullOrWhiteSpace(ReadString(obj, "sessionId")))
            obj["sessionId"] = BuildLiveSessionId(ReadString(obj, "source"), ReadString(obj, "device"));

        if (string.IsNullOrWhiteSpace(ReadString(obj, "mode")))
            obj["mode"] = "live";
    }

    return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
}

static JsonNode? TryParseJsonNode(string payload)
{
    try
    {
        return JsonNode.Parse(payload);
    }
    catch
    {
        return null;
    }
}

static string ReadString(JsonObject obj, string key)
{
    if (!obj.TryGetPropertyValue(key, out JsonNode? node) || node == null)
        return string.Empty;

    try
    {
        return node.GetValue<string>() ?? string.Empty;
    }
    catch
    {
        return node.ToString().Trim('\"');
    }
}

static bool TryReadFloat(JsonObject obj, string key, out float value)
{
    value = 0f;
    if (!obj.TryGetPropertyValue(key, out JsonNode? node) || node == null)
        return false;

    try
    {
        value = node.GetValue<float>();
        return true;
    }
    catch
    {
        string text = node.ToString().Trim('\"');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

static string BuildLiveSessionId(string source, string device)
{
    string id = !string.IsNullOrWhiteSpace(source) ? source : device;
    if (string.IsNullOrWhiteSpace(id))
        id = "watch";

    id = id.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
    return $"live-{id}";
}

sealed record BridgeConfig(int BridgePort, string UnityIp, int UnityPort, string LogDirectory)
{
    public static BridgeConfig FromArgs(string[] args)
    {
        int bridgePort = ReadInt(args, "--bridge-port", "FIT3_BRIDGE_PORT", 7777);
        string unityIp = ReadString(args, "--unity-ip", "UNITY_IP", "127.0.0.1");
        int unityPort = ReadInt(args, "--unity-port", "UNITY_PORT", 5055);
        string logDir = ReadString(args, "--log-dir", "FIT3_LOG_DIR", @"C:\Fit3UnityBridge\Logs");

        if (!IPAddress.TryParse(unityIp, out _))
            throw new ArgumentException($"Invalid Unity IP address: {unityIp}");

        if (bridgePort is < 1 or > 65535)
            throw new ArgumentException($"Invalid bridge port: {bridgePort}");

        if (unityPort is < 1 or > 65535)
            throw new ArgumentException($"Invalid Unity port: {unityPort}");

        return new BridgeConfig(bridgePort, unityIp, unityPort, logDir);
    }

    private static int ReadInt(string[] args, string flag, string envName, int fallback)
    {
        string text = ReadString(args, flag, envName, fallback.ToString());
        return int.TryParse(text, out int value) ? value : fallback;
    }

    private static string ReadString(string[] args, string flag, string envName, string fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        string? env = Environment.GetEnvironmentVariable(envName);
        return string.IsNullOrWhiteSpace(env) ? fallback : env;
    }
}
