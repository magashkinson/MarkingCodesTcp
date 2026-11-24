using System.Net.Sockets;
using System.Text;
using MarkingCodes.Protocol;

namespace MarkingCodes.Client;

public sealed class MarkingCodeClient(string host, int port, string outputPath, ClientOptions options)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await using var writer = OpenOutput(outputPath);
        Log.Info($"Writing codes to {Path.GetFullPath(outputPath)}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ReceiveAsync(writer, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is SocketException or IOException or InvalidDataException or TimeoutException)
            {
                Log.Info($"Connection lost: {ex.Message}");
            }

            Log.Info($"Reconnecting in {options.ReconnectDelay.TotalSeconds:0.#} s...");
            try
            {
                await Task.Delay(options.ReconnectDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        Log.Info("Client stopped");
    }

    private async Task ReceiveAsync(StreamWriter writer, CancellationToken ct)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port, ct);
        Log.Info($"Connected to {host}:{port}");
        var stream = tcp.GetStream();

        while (true)
        {
            var code = await ReadWithIdleTimeoutAsync(stream, ct);
            if (code is null)
            {
                Log.Info("Server closed the connection");
                return;
            }
            await writer.WriteLineAsync(code.AsMemory(), ct);
        }
    }

    // если сервер пропал без FIN, сам TCP об этом не узнает - рвем по таймауту
    private async Task<string?> ReadWithIdleTimeoutAsync(Stream stream, CancellationToken ct)
    {
        using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idleCts.CancelAfter(options.IdleTimeout);
        try
        {
            return await FrameProtocol.ReadAsync(stream, idleCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"No data from server for {options.IdleTimeout.TotalSeconds:0.#} s.");
        }
    }

    private static StreamWriter OpenOutput(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
            Directory.CreateDirectory(directory);
        var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        return new StreamWriter(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }
}

public sealed record ClientOptions(TimeSpan ReconnectDelay, TimeSpan IdleTimeout)
{
    public static ClientOptions Default { get; } = new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
}
