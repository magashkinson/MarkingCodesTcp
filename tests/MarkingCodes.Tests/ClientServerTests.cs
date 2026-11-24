using System.Net;
using MarkingCodes.Client;
using MarkingCodes.Server;

namespace MarkingCodes.Tests;

public class ClientServerTests : IDisposable
{
    private readonly string _output = Path.Combine(Path.GetTempPath(), $"codes-{Guid.NewGuid():N}.txt");
    private static readonly ClientOptions FastClient = new(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2));
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);

    [Fact]
    public async Task Client_ReconnectsAfterServerRestart_AndKeepsAppending()
    {
        var counter = 0;
        string Next() => $"code-{Interlocked.Increment(ref counter)}";

        using var clientCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (server, serverCts, serverTask) = StartServer(0, Next);
        var port = server.LocalEndPoint.Port;
        var clientTask = new MarkingCodeClient("127.0.0.1", port, _output, FastClient).RunAsync(clientCts.Token);

        await WaitForLinesAsync(5);
        await serverCts.CancelAsync();
        await serverTask;
        server.Dispose();
        var linesBeforeRestart = ReadLines().Length;

        (server, serverCts, serverTask) = StartServer(port, Next);
        await WaitForLinesAsync(linesBeforeRestart + 5);

        await clientCts.CancelAsync();
        await clientTask;
        await serverCts.CancelAsync();
        await serverTask;
        server.Dispose();

        var lines = ReadLines();
        Assert.All(lines, l => Assert.Matches(@"^code-\d+$", l));
        Assert.Equal(lines.Length, lines.Distinct().Count());
    }

    [Fact]
    public async Task ServerKeepsServing_AfterClientDisconnects()
    {
        using var serverCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var server = new MarkingCodeServer(new IPEndPoint(IPAddress.Loopback, 0), () => "x", Interval);
        server.Start();
        var serverTask = server.RunAsync(serverCts.Token);

        for (var session = 1; session <= 2; session++)
        {
            using var clientCts = new CancellationTokenSource();
            var clientTask = new MarkingCodeClient("127.0.0.1", server.LocalEndPoint.Port, _output, FastClient)
                .RunAsync(clientCts.Token);
            await WaitForLinesAsync(session * 3);
            await clientCts.CancelAsync();
            await clientTask;
        }

        await serverCts.CancelAsync();
        await serverTask;
    }

    private static (MarkingCodeServer, CancellationTokenSource, Task) StartServer(int port, Func<string> next)
    {
        var server = new MarkingCodeServer(new IPEndPoint(IPAddress.Loopback, port), next, Interval);
        server.Start();
        var cts = new CancellationTokenSource();
        return (server, cts, server.RunAsync(cts.Token));
    }

    private string[] ReadLines()
    {
        if (!File.Exists(_output)) return [];
        using var reader = new StreamReader(new FileStream(_output, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task WaitForLinesAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (ReadLines().Length < count)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Expected {count} lines, got {ReadLines().Length}.");
            await Task.Delay(20);
        }
    }

    public void Dispose() => File.Delete(_output);
}
