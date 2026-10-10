namespace LunaPlayer.YouTube.Playback;

/// <summary>Where a list of rows came from.</summary>
internal enum SessionKind
{
    /// <summary>A search, which can be asked for more.</summary>
    Search,
    /// <summary>A playlist, which arrives whole.</summary>
    Playlist,
    /// <summary>A channel browser, whose tabs are each paged on their own and kept once loaded.</summary>
    Channel,
}

/// <summary>A list of videos the user is working through, and everything that follows from it.</summary>
///
/// <remarks>
/// One of these exists from the moment a search or a playlist comes back until the user opens another or
/// closes the window. It is what makes the next video after this one a meaningful idea: the playlist holds
/// stream addresses, which say nothing about order or about what comes next, and this holds the order the
/// results window showed.
///
/// A channel is several lists at once - videos, shorts, streams and the rest - and this keeps each tab it
/// has visited, its rows and its paging both, so switching back to a tab is instant rather than another
/// fetch. A search or a playlist is one list, which is simply the single tab that is always current.
///
/// The two qualities are frozen when it is made. Changing a quality setting halfway through should not
/// leave the first half of a list resolved one way and the second another, and the cache is keyed on them,
/// so a session that read them afresh each time would quietly stop finding its own prefetches. Which of the
/// two a play uses - picture or sound - is not frozen: it is chosen at the moment of playing, and both are
/// prefetched, so either is ready at once.
/// </remarks>
internal sealed class Session : IDisposable
{
    /// <summary>One tab's rows and the source they are paged from. A search or a playlist has exactly one;
    /// a channel has one per section it has opened.</summary>
    private sealed class TabState
    {
        internal List<YouTubeResult> Items { get; } = [];
        internal IResultPage? Page { get; set; }
        internal bool Exhausted { get; set; }
        internal int Selected { get; set; }
    }

    private readonly Dictionary<int, TabState> _tabs = [];
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>Taken while the source is alive and kept.</summary>
    /// <remarks>
    /// <c>CancellationTokenSource.Token</c> throws once the source is disposed, and a session can be
    /// disposed while a window that was opened on it is still on screen. A token read afterwards is not a
    /// problem in itself - it simply reads as cancelled, which is exactly right - so it is captured here
    /// rather than left to throw at whichever caller happens to ask last.
    /// </remarks>
    private readonly CancellationToken _token;

    internal Session(
        SessionKind kind,
        IEnumerable<YouTubeResult> items,
        int videoQuality,
        int audioQuality,
        IResultPage? page = null,
        string channelBase = "",
        int currentTab = 0,
        string channelTitle = "")
    {
        Kind = kind;
        VideoQuality = videoQuality;
        AudioQuality = audioQuality;
        ChannelBase = channelBase;
        CurrentTab = currentTab;
        ChannelTitle = channelTitle;
        _token = _cancellation.Token;
        var initial = new TabState { Page = page };
        initial.Items.AddRange(items);
        _tabs[currentTab] = initial;
    }

    private TabState Active => _tabs[CurrentTab];

    internal SessionKind Kind { get; }

    /// <summary>The heading above the list. It names what kind of list this is and nothing more: the window
    /// is opened straight from the search box, so what was searched for is
    /// still the last thing the user typed.</summary>
    internal string Label => Kind switch
    {
        // Translators: Heading above the list of videos a YouTube search found.
        SessionKind.Search => Tr("Search results"),
        // A channel names itself when it is known; the generic word covers the rare case where it is not.
        SessionKind.Channel => ChannelTitle.Length > 0
            ? ChannelTitle
            // Translators: Heading above a YouTube channel's browser when the channel's name is not known.
            : Tr("Channel"),
        // Translators: Heading above the list of videos in a YouTube playlist.
        _ => Tr("Playlist videos"),
    };

    /// <summary>The rows of the tab that is current.</summary>
    internal IReadOnlyList<YouTubeResult> Items => Active.Items;

    /// <summary>The picture height a video play resolves at.</summary>
    internal int VideoQuality { get; }

    /// <summary>The bitrate an audio play resolves at.</summary>
    internal int AudioQuality { get; }

    /// <summary>Whether the next play is the picture or the sound alone. Set by the results window from the
    /// key the user pressed - Enter for video, Ctrl+Enter for audio - just before the play is asked for.
    /// </summary>
    internal PlayMode Mode { get; set; } = PlayMode.Video;

    /// <summary>Whether a mode resolves the audio stream alone. The word the resolve and the cache still
    /// speak in.</summary>
    internal static bool AudioFor(PlayMode mode) => mode is PlayMode.Audio;

    /// <summary>The quality a mode resolves at: the audio bitrate for sound, the picture height otherwise.
    /// </summary>
    internal int QualityFor(PlayMode mode) => mode is PlayMode.Audio ? AudioQuality : VideoQuality;

    /// <summary>The source the current tab is paged from, held so it can be asked for more. Null for a
    /// playlist, which has no more to give; a search page for a search; a channel-tab page for a channel.
    /// </summary>
    internal IResultPage? Page => Active.Page;

    /// <summary>Whether the current tab has given everything it has, so no more should be asked for.
    /// </summary>
    internal bool Exhausted { get => Active.Exhausted; set => Active.Exhausted = value; }

    /// <summary>The canonical channel address a channel session's tabs are built from. Empty otherwise.
    /// </summary>
    internal string ChannelBase { get; }

    /// <summary>Which channel tab is showing, as an index into <see cref="Client.Tabs.Keys"/>.</summary>
    internal int CurrentTab { get; private set; }

    /// <summary>The channel's name, shown as the heading of a channel session. Empty otherwise.</summary>
    internal string ChannelTitle { get; }

    /// <summary>Which row the user was on in the current tab. Kept so closing the results window and coming
    /// back to it lands where they left rather than at the top.</summary>
    internal int Selected { get => Active.Selected; set => Active.Selected = value; }

    /// <summary>Cancelled when the session ends, abandoning every resolve started on its behalf.</summary>
    /// <remarks>
    /// A source rather than a plain flag, which cannot be reset once set and so poisons a
    /// session that outlives its first cancellation. This one is owned by the session and dies with it, so
    /// that state is unreachable.
    /// </remarks>
    internal CancellationToken Token => _token;

    internal bool IsCancelled => _token.IsCancellationRequested;

    /// <summary>Whether a channel tab has already been loaded and can be shown again without a fetch.
    /// </summary>
    internal bool HasTab(int index) => _tabs.ContainsKey(index);

    /// <summary>Makes an already-loaded tab the current one. No fetch: this is what makes returning to a
    /// tab free.</summary>
    internal void ActivateTab(int index)
    {
        if (_tabs.ContainsKey(index))
            CurrentTab = index;
    }

    /// <summary>Keeps a freshly-loaded tab's rows and page, and makes it the current one.</summary>
    internal void CacheTab(int index, IEnumerable<YouTubeResult> items, IResultPage? page)
    {
        var state = new TabState { Page = page };
        state.Items.AddRange(items);
        _tabs[index] = state;
        CurrentTab = index;
    }

    /// <summary>Adds a page of results to the end of the current tab.</summary>
    internal void Append(IEnumerable<YouTubeResult> items) => Active.Items.AddRange(items);

    /// <summary>Where in the current tab a video is, or -1.</summary>
    internal int IndexOf(string watchUrl)
        => Active.Items.FindIndex(item => string.Equals(item.Url, watchUrl, StringComparison.Ordinal));

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        // Every tab holds a live response from YouTube. Closing them waits for a batch still being taken -
        // advancing and disposing the same source at once is undefined - but nothing here waits for that,
        // because nobody is reading a list that has ended.
        foreach (var tab in _tabs.Values)
            if (tab.Page is IAsyncDisposable page)
                _ = page.DisposeAsync().AsTask();
    }
}
