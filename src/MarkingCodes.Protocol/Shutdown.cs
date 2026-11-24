using System.Runtime.InteropServices;

namespace MarkingCodes.Protocol;

public sealed class Shutdown : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly PosixSignalRegistration _sigterm;

    public Shutdown()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
        _sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
        {
            ctx.Cancel = true;
            _cts.Cancel();
        });
    }

    public CancellationToken Token => _cts.Token;

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _cts.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _sigterm.Dispose();
        _cts.Dispose();
    }
}
