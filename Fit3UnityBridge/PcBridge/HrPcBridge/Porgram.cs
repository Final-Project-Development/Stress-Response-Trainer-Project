using System.Net;
using System.Net.Sockets;
using System.Text;

const int bridgePort = 7777;
const int unityForwardPort = 5055;

string logDirectory = @"C:\Fit3UnityBridge\Logs";
Directory.CreateDirectory(logDirectory);

string logPath = Path.Combine(logDirectory, "hr_log.jsonl");

using var unitySender = new UdpClient();
var unityEndPoint = new IPEndPoint(IPAddress.Loopback, unityForwardPort);

var logLock = new SemaphoreSlim(1, 1);
var unitySendLock = new SemaphoreSlim(1, 1);

Console.WriteLine("Fit3 HR PC Bridge");
Console.WriteLine("-----------------");
Console.WriteLine($"UDP listen port: {bridgePort}  [Android test packets]");
Console.WriteLine($"TCP listen port: {bridgePort}  [post-workout HR timeline]");
Console.WriteLine($"Forwarding to Unity UDP: 127.0.0.1:{unityForwardPort}");
Console.WriteLine($"Logging to: {logPath}");
Console.WriteLine();

await Task.WhenAll(
    RunUdpListenerAsync(),
    RunTcpListenerAsync()
);

async Task RunUdpListenerAsync()
{
    using var udpReceiver = new UdpClient(bridgePort);

    Console.WriteLine($"UDP listener started on port {bridgePort}");

    while (true)
    {
        var result = await udpReceiver.ReceiveAsync();

        string json = Encoding.UTF8.GetString(result.Buffer).Trim();

        await ProcessPayloadAsync(
            json: json,
            transport: "udp",
            remote: result.RemoteEndPoint.ToString()
        );
    }
}

async Task RunTcpListenerAsync()
{
    var tcpListener = new TcpListener(IPAddress.Any, bridgePort);
    tcpListener.Start();

    Console.WriteLine($"TCP listener started on port {bridgePort}");

    while (true)
    {
        var client = await tcpListener.AcceptTcpClientAsync();

        _ = Task.Run(async () =>
        {
            await HandleTcpClientAsync(client);
        });
    }
}

async Task HandleTcpClientAsync(TcpClient client)
{
    using (client)
    {
        string remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        using var reader = new StreamReader(
            client.GetStream(),
            Encoding.UTF8
        );

        while (true)
        {
            string? line = await reader.ReadLineAsync();

            if (line == null)
                break;

            line = line.Trim();

            if (line.Length == 0)
                continue;

            await ProcessPayloadAsync(
                json: line,
                transport: "tcp",
                remote: remote
            );
        }
    }
}

async Task ProcessPayloadAsync(
    string json,
    string transport,
    string remote
)
{
    string pcReceivedAt = DateTimeOffset.UtcNow.ToString("O");

    string logLine =
        $"{{\"pcReceivedAt\":\"{pcReceivedAt}\",\"transport\":\"{transport}\",\"remote\":\"{JsonEscape(remote)}\",\"payload\":{json}}}";

    Console.WriteLine(logLine);

    await logLock.WaitAsync();
    try
    {
        await File.AppendAllTextAsync(
            logPath,
            logLine + Environment.NewLine
        );
    }
    finally
    {
        logLock.Release();
    }

    byte[] bytes = Encoding.UTF8.GetBytes(json);

    await unitySendLock.WaitAsync();
    try
    {
        await unitySender.SendAsync(
            bytes,
            bytes.Length,
            unityEndPoint
        );
    }
    finally
    {
        unitySendLock.Release();
    }
}

string JsonEscape(string value)
{
    return value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");
}