using System.Net;
using System.Net.Sockets;
using MarkingCodes.Protocol;

namespace MarkingCodes.Server;

public sealed class MarkingCodeServer(IPEndPoint endPoint, Func<string> generateCode, TimeSpan interval) : IDisposable
{
    private readonly TcpListener _listener = new(endPoint);

    public IPEndPoint LocalEndPoint => (IPEndPoint)_listener.LocalEndpoint;

    public void Start() => _listener.Start();

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info($"Server listening on {LocalEndPoint}");
        var connections = new List<Task>();
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                connections.RemoveAll(t => t.IsCompleted);
                connections.Add(ServeClientAsync(client, ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            _listener.Stop();
            await Task.WhenAll(connections);
            Log.Info("Server stopped");
        }
    }

    private async Task ServeClientAsync(TcpClient client, CancellationToken serverCt)
    {
        using var _ = client;
        var remote = client.Client.RemoteEndPoint;
        Log.Info($"Client connected: {remote}");

        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
        var stream = client.GetStream();
        // запись в отвалившийся сокет может не упасть, поэтому отключение ловим по чтению
        var watchTask = WatchForDisconnectAsync(stream, connectionCts);
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(connectionCts.Token))
                await FrameProtocol.WriteAsync(stream, generateCode(), connectionCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            Log.Info($"Connection error with {remote}: {ex.Message}");
        }
        finally
        {
            await connectionCts.CancelAsync();
            await watchTask;
            Log.Info($"Client disconnected: {remote}");
        }
    }

    private static async Task WatchForDisconnectAsync(Stream stream, CancellationTokenSource connectionCts)
    {
        var buffer = new byte[1];
        try
        {
            while (await stream.ReadAsync(buffer, connectionCts.Token) > 0) { }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
        }
        await connectionCts.CancelAsync();
    }

    public void Dispose() => _listener.Dispose();
}
