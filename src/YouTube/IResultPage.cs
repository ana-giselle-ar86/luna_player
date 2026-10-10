namespace LunaPlayer.YouTube;

/// <summary>A source of results that can be drawn from a batch at a time.</summary>
///
/// <remarks>
/// The results window pages by asking for more; whether the more comes from a PyYt search continuation or a
/// yt-dlp channel tab is not its concern. Both a <see cref="SearchPage"/> and a
/// <see cref="ChannelTabPage"/> answer to this, so one paging path in the window and the session serves
/// both.
/// </remarks>
internal interface IResultPage
{
    /// <summary>Whether another batch might still be waiting. False once a batch has come back short.</summary>
    bool HasMore { get; }

    /// <summary>Fetches up to <paramref name="count"/> more results, an empty list at the end.</summary>
    Task<IReadOnlyList<YouTubeResult>> Take(int count, CancellationToken token);
}

/// <summary>Pages one tab of a YouTube channel through yt-dlp's flat extraction.</summary>
///
/// <remarks>
/// yt-dlp is asked for a fixed window of the tab at a time (<see cref="Batch"/> rows, addressed 1-based and
/// inclusive), and the window is advanced
/// by that same fixed count each turn. A window that comes back with fewer raw entries than it asked for is
/// the last one, so <see cref="HasMore"/> falls false and no needless empty fetch follows. A lock keeps two
/// overlapping <see cref="Take"/> calls from reading the same window twice.
/// </remarks>
internal sealed class ChannelTabPage : IResultPage, IAsyncDisposable
{
    internal const int Batch = 30;

    private readonly YtDlpClient _ytDlp;
    private readonly string _channelBase;
    private readonly string _tabKey;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private int _next = 1;
    private bool _exhausted;

    internal ChannelTabPage(YtDlpClient ytDlp, string channelBase, string tabKey)
    {
        _ytDlp = ytDlp;
        _channelBase = channelBase;
        _tabKey = tabKey;
    }

    public bool HasMore => !_exhausted;

    public async Task<IReadOnlyList<YouTubeResult>> Take(int count, CancellationToken token)
    {
        var found = new List<YouTubeResult>();
        await _turn.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // A window may map to fewer playable rows than it holds - a community post, a private video -
            // so keep pulling windows until enough rows are gathered or the tab runs dry.
            while (found.Count < count && !_exhausted)
            {
                token.ThrowIfCancellationRequested();
                var start = _next;
                var end = start + Batch - 1;
                var (items, raw) = _ytDlp.ChannelTab(_channelBase, _tabKey, start, end, token);
                _next = end + 1;
                found.AddRange(items);
                if (raw < Batch)
                    _exhausted = true;
            }
        }
        finally
        {
            _turn.Release();
        }
        return found;
    }

    public ValueTask DisposeAsync()
    {
        _turn.Dispose();
        return ValueTask.CompletedTask;
    }
}
