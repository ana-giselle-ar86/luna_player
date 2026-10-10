namespace LunaPlayer.YouTube;

/// <summary>Resolves videos ahead of when they are wanted, and hands out what it has already resolved.
/// </summary>
///
/// <remarks>
/// Keeping two dictionaries for this - one of futures and one of finished results - and moving entries
/// between them would leave a window where a video is in neither and a second request for it starts a
/// second download. One <see cref="Task"/> per video closes that window: the task is the entry whether it
/// has finished or not, so "already done", "already running" and "not started" are one
/// lookup rather than two.
///
/// A plain dictionary behind a lock rather than a <c>ConcurrentDictionary</c>, deliberately.
/// <c>GetOrAdd</c> runs its factory outside the lock, so two threads racing the same video would both
/// start a request - the very thing this exists to prevent. There is no contention to avoid here anyway:
/// a handful of touches per keypress.
/// </remarks>
internal sealed class ResolveCache : IDisposable
{
    /// <summary>How many videos may be resolved at once. Four, because going wider mostly buys a faster
    /// route to being rate-limited.</summary>
    private const int Workers = 4;

    /// <summary>How many entries to keep. Enough that working up and down a
    /// long playlist never throws away a video that was already resolved, and small enough that a session
    /// left open all day does not accumulate addresses that expired hours ago.</summary>
    private const int Bound = 200;

    private readonly Lock _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(Workers, Workers);
    private readonly YtDlpClient _ytDlp;
    private long _clock;
    private bool _disposed;

    internal ResolveCache(YtDlpClient ytDlp)
    {
        _ytDlp = ytDlp;
    }

    /// <summary>Resolves one video through yt-dlp, the only thing that can turn it into a playable
    /// address.</summary>
    private ResolveOutcome Resolve(
        string watchUrl, YouTubeResult item, bool audioOnly, int quality, CancellationToken token)
        => _ytDlp.Resolve(watchUrl, item, audioOnly, quality, token);

    /// <summary>The name one set of options gives a video.</summary>
    /// <remarks>
    /// Two requests that would resolve differently must not share an entry, which is why the options are
    /// part of the name: the same video wanted as sound alone and as picture resolves to different
    /// addresses, and one must not be handed back for the other.
    /// </remarks>
    internal string Key(string watchUrl, bool audioOnly, int quality)
        => $"{watchUrl}|a={(audioOnly ? 1 : 0)}|q={quality}";

    /// <summary>What has already been resolved for a video, or null. Never starts work and never blocks,
    /// so it is safe on the UI thread - which is the point of it: it decides whether a video can be played
    /// with no progress window at all.</summary>
    internal Resolved? TryTake(string key)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out var entry) || !entry.Task.IsCompletedSuccessfully)
                return null;
            var resolved = entry.Task.Result.Value;
            if (resolved is null)
                return null;
            // An address that has expired is worse than none: it would be handed to mpv and fail there,
            // where the failure looks like a broken video rather than a stale link.
            if (!resolved.IsFresh)
            {
                _entries.Remove(key);
                return null;
            }
            entry.Touched = ++_clock;
            return resolved;
        }
    }

    /// <summary>Starts resolving a video, or joins the attempt already running.</summary>
    /// <param name="token">The session's. Cancelling it abandons the work itself, which is what ending a
    /// session should do; a caller that only wants to stop waiting passes its own token to
    /// <see cref="Wait"/> instead.</param>
    internal Task<ResolveOutcome> Start(
        string key,
        string watchUrl,
        YouTubeResult item,
        bool audioOnly,
        int quality,
        CancellationToken token)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_entries.TryGetValue(key, out var existing) && !Stale(existing))
            {
                existing.Touched = ++_clock;
                return existing.Task;
            }
            var entry = new Entry(Run(watchUrl, item, audioOnly, quality, token)) { Touched = ++_clock };
            _entries[key] = entry;
            Evict();
            return entry.Task;
        }
    }

    /// <summary>Starts resolving a video and forgets about it. What prefetching is.</summary>
    internal void Prefetch(
        string watchUrl, YouTubeResult item, bool audioOnly, int quality, CancellationToken token)
    {
        if (_disposed || token.IsCancellationRequested)
            return;
        var key = Key(watchUrl, audioOnly, quality);
        if (TryTake(key) is not null)
            return;
        _ = Start(key, watchUrl, item, audioOnly, quality, token);
    }

    /// <summary>Waits for a video to resolve, joining a prefetch already under way rather than asking
    /// again.</summary>
    ///
    /// <remarks>
    /// Two tokens, and they mean different things. <paramref name="token"/> is the session's and stops the
    /// work; <paramref name="waitToken"/> is the progress window's Cancel button and stops only the
    /// waiting, leaving the resolve to finish into the cache - so a user who gives up and then changes
    /// their mind gets the video instantly rather than starting again.
    /// </remarks>
    internal ResolveOutcome Wait(
        string watchUrl,
        YouTubeResult item,
        bool audioOnly,
        int quality,
        CancellationToken token,
        CancellationToken waitToken)
    {
        var key = Key(watchUrl, audioOnly, quality);
        Task<ResolveOutcome> task;
        try
        {
            task = Start(key, watchUrl, item, audioOnly, quality, token);
        }
        catch (ObjectDisposedException)
        {
            // The player is shutting down. Nothing is waiting to be told about it.
            return ResolveOutcome.Cancelled;
        }
        try
        {
            task.Wait(waitToken);
        }
        catch (OperationCanceledException)
        {
            return ResolveOutcome.Cancelled;
        }
        catch (Exception failure)
        {
            // Task.Wait rethrows a faulted task wrapped in an AggregateException. Letting that out would
            // fault the job it runs inside, and a faulted job is rethrown on the UI thread as a crash -
            // the wrong end for something as ordinary as a request that went wrong.
            return PyYtClient.Explain(failure.InnerException ?? failure, waitToken);
        }
        return task.IsCompletedSuccessfully ? task.Result : ResolveOutcome.Cancelled;
    }

    private async Task<ResolveOutcome> Run(
        string watchUrl, YouTubeResult item, bool audioOnly, int quality, CancellationToken token)
    {
        try
        {
            await _gate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ResolveOutcome.Cancelled;
        }
        try
        {
            // Off the calling thread and off the pool's scheduler for the duration: the resolve blocks on
            // a web request, and the caller may be the UI thread arranging a prefetch.
            return await Task.Run(
                () => Resolve(watchUrl, item, audioOnly, quality, token), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ResolveOutcome.Cancelled;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Whether an entry is worth keeping. A failure is not: retrying is what the user expects
    /// from pressing Play again, and a cached refusal would make the second attempt as fruitless as the
    /// first.</summary>
    private static bool Stale(Entry entry)
    {
        if (!entry.Task.IsCompleted)
            return false;
        if (!entry.Task.IsCompletedSuccessfully)
            return true;
        var resolved = entry.Task.Result.Value;
        return resolved is null || !resolved.IsFresh;
    }

    /// <remarks>
    /// Only finished entries are evicted. One still running may have somebody waiting on it, and dropping
    /// it from the table would not stop the work - it would only mean the next request started it again.
    /// </remarks>
    private void Evict()
    {
        while (_entries.Count > Bound)
        {
            var oldest = _entries
                .Where(pair => pair.Value.Task.IsCompleted)
                .OrderBy(pair => pair.Value.Touched)
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (oldest is null)
                return;
            _entries.Remove(oldest);
        }
    }

    /// <remarks>
    /// The gate is deliberately not disposed. Resolves may still be in flight at shutdown - their session
    /// has been cancelled but the request they are inside has not returned yet - and each of them releases
    /// the gate on its way out. Disposing it under them turns an orderly shutdown into an
    /// <see cref="ObjectDisposedException"/> on a pool thread. A <see cref="SemaphoreSlim"/> that has never
    /// been asked for its wait handle holds nothing that needs releasing, which is the case here.
    /// </remarks>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _entries.Clear();
        }
    }

    /// <param name="Task">The resolve. Started once and shared by everybody who asks for the same video
    /// under the same options.</param>
    private sealed record Entry(Task<ResolveOutcome> Task)
    {
        /// <summary>When this was last asked for, on a counter rather than a clock: eviction only needs
        /// the order, and a counter cannot go backwards the way a system clock can.</summary>
        internal long Touched { get; set; }
    }
}
