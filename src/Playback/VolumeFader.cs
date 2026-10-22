namespace LunaPlayer.Playback;

/// <summary>Starts sample-clocked ramps in mpv. Polling only waits for audible completion; it never
/// writes intermediate gains or advances a ramp while the output is paused or starved.</summary>
internal sealed class VolumeFader : IDisposable
{
    private readonly Lock _sync = new();
    private readonly Func<double, double, bool> _apply;
    private readonly Func<State?> _read;
    private CancellationTokenSource? _cancellation;
    private bool _disposed;

    internal readonly record struct State(double Gain, bool Active, bool Supported);

    internal VolumeFader(Func<double, double, bool> apply, Func<State?> read)
    {
        _apply = apply;
        _read = read;
    }

    internal double Level
    {
        get { lock (_sync) return _disposed ? 1 : _read()?.Gain ?? 1; }
    }

    internal void Set(double level)
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            Cancel();
            _apply(Math.Clamp(level, 0, 1), 0);
        }
    }

    internal void FadeTo(double target, int milliseconds, Action? completed = null)
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            Cancel();
            // Older libmpv builds and passthrough outputs cannot apply a PCM envelope. Keep their
            // transport usable without falling back to the block-sized volume jumps this replaces.
            if (_read() is not { Supported: true }
                || !_apply(Math.Clamp(target, 0, 1), Math.Clamp(milliseconds / 1000.0, 0, 60)))
            {
                _apply(Math.Clamp(target, 0, 1), 0);
                completed?.Invoke();
                return;
            }
            if (completed is null)
                return;
            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            _ = WaitForOutput(cancellation, cancellation.Token, completed);
        }
    }

    private async Task WaitForOutput(CancellationTokenSource cancellation, CancellationToken token,
        Action completed)
    {
        try
        {
            while (true)
            {
                await Task.Delay(10, token).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_disposed || !ReferenceEquals(_cancellation, cancellation))
                        return;
                    if (_read() is { Active: true, Supported: true })
                        continue;
                    _cancellation = null;
                    // Replacement commands and completion are serialized, so a cancelled pause or
                    // seek cannot run after a newer transport request has taken over the envelope.
                    completed();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A replacement transition or disposal cancels this completion callback.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void Cancel()
    {
        _cancellation?.Cancel();
        _cancellation = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            Cancel();
        }
    }
}
