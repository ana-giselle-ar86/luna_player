using System.Diagnostics;

namespace LunaPlayer.Playback;

/// <summary>Runs one cancellable volume ramp. Starting another ramp supersedes the old one, which keeps
/// rapid seeks and repeated transport commands from leaving playback at an intermediate volume.</summary>
internal sealed class VolumeFader : IDisposable
{
    private const int FrameMilliseconds = 16;
    private readonly Lock _sync = new();
    private readonly Action<double> _apply;
    private CancellationTokenSource? _cancellation;
    private double _level = 1;
    private bool _disposed;

    internal VolumeFader(Action<double> apply)
    {
        _apply = apply;
    }

    internal double Level
    {
        get { lock (_sync) return _level; }
    }

    internal void Set(double level)
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _level = Math.Clamp(level, 0, 1);
            _apply(_level);
        }
    }

    internal void FadeTo(double target, int milliseconds, Action? completed = null, Func<bool>? startWhen = null)
    {
        target = Math.Clamp(target, 0, 1);
        CancellationTokenSource cancellation;
        CancellationToken token;
        double start;
        lock (_sync)
        {
            if (_disposed)
                return;
            if (milliseconds <= 0 || target == _level)
            {
                Set(target);
                completed?.Invoke();
                return;
            }
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            token = cancellation.Token;
            _cancellation = cancellation;
            start = _level;
        }
        // Keep a constant rate when a fade is reversed part-way through. The configured interval is the
        // time needed to travel the full volume range, not a new full delay for every partial ramp.
        var duration = Math.Max(1, (int)Math.Round(milliseconds * Math.Abs(target - start)));
        _ = Run(start, target, duration, cancellation, token, completed, startWhen);
    }

    private async Task Run(double start, double target, int milliseconds,
        CancellationTokenSource cancellation, CancellationToken token, Action? completed, Func<bool>? startWhen)
    {
        try
        {
            // Output may still be starting after unpause. Keep the initial level until audio advances;
            // otherwise part of a fade-in is spent waiting for the device rather than fading audio.
            while (startWhen is not null && !startWhen())
                await Task.Delay(FrameMilliseconds, token).ConfigureAwait(false);

            var started = Stopwatch.GetTimestamp();
            while (true)
            {
                var remaining = milliseconds - Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (remaining > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(FrameMilliseconds, remaining)), token)
                        .ConfigureAwait(false);
                lock (_sync)
                {
                    if (_disposed || !ReferenceEquals(_cancellation, cancellation))
                        return;
                    // Measure the whole transition, including volume updates and scheduling delays.
                    // Counting steps adds that overhead to every frame and stretches the interval.
                    var fraction = Math.Min(1, Stopwatch.GetElapsedTime(started).TotalMilliseconds / milliseconds);
                    _level = fraction >= 1 ? target : start + (target - start) * fraction;
                    // Applying a frame is part of the transition: a replacement ramp must not be
                    // followed by an old frame or completion callback that changes playback again.
                    _apply(_level);
                    if (fraction >= 1)
                    {
                        _cancellation = null;
                        completed?.Invoke();
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A new fade or immediate volume change cancels the previous transition by design.
        }
        finally
        {
            lock (_sync)
                cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }
}
