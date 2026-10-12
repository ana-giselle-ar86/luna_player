namespace LunaPlayer.YouTube;

/// <summary>The operations on a single video that need no session behind them.</summary>
///
/// <remarks>
/// Playing a list of videos lives in <see cref="Playback.Sessions"/>, because it needs the player and the
/// order the results window showed. What is left here is the work that answers about one address and then
/// finishes: reading what the uploader wrote under it, listing a playlist, and opening a channel tab. Saving
/// a video is the download engine's job now.
///
/// Reading about a video - its description, or the contents of a playlist - goes through the metadata
/// client, which needs nothing installed. A channel tab goes through yt-dlp, which the user has to fetch
/// first; when the programs it needs are not there, the user is told so rather than quietly getting nothing.
/// </remarks>
internal sealed class Backend
{
    private readonly Metadata.Client _client;
    private readonly Client.Channels _channels;

    internal Backend(Metadata.Client client, Client.Channels channels)
    {
        _client = client;
        _channels = channels;
    }

    /// <summary>Whether the programs yt-dlp needs have been fetched.</summary>
    internal static bool HasComponents => Utils.HasAll;

    /// <summary>The text the uploader wrote under a video.</summary>
    /// <remarks>Runs on a worker thread, so the failure it reports is a code and a raw detail; the
    /// sentence the user reads is chosen back on the UI thread.</remarks>
    internal (string? Text, ResolveFailure Failure, string Detail) Describe(
        string watchUrl, CancellationToken token)
    {
        try
        {
            return (_client.Description(watchUrl, token), ResolveFailure.None, string.Empty);
        }
        catch (Exception failure)
        {
            var explained = FailureMapping.Explain(failure, token);
            return (null, explained.Failure, explained.Detail);
        }
    }

    /// <summary>Every video in a playlist, and what the playlist is called.</summary>
    /// <remarks>
    /// Through the metadata client, which reads the listing without needing anything installed - the same
    /// as a search, and the same library. Only turning a video into a playable file needs yt-dlp.
    /// </remarks>
    internal (string Title, IReadOnlyList<YouTubeResult> Items, ResolveFailure Failure, string Detail) Playlist(
        string link, CancellationToken token)
    {
        try
        {
            var (title, items) = _client.Playlist(link, token);
            return (title, items, ResolveFailure.None, string.Empty);
        }
        catch (Exception failure)
        {
            var explained = FailureMapping.Explain(failure, token);
            return (string.Empty, [], explained.Failure, explained.Detail);
        }
    }

    /// <summary>The words YouTube offers to finish what the user has typed, for the search box's live
    /// suggestions.</summary>
    /// <remarks>
    /// Through the metadata client, needing nothing installed. Called on the background thread the search
    /// dialog starts on each keystroke; a failed or empty fetch is simply no suggestions, so anything that
    /// goes wrong reads as an empty list rather than an error the box has no way to show.
    /// </remarks>
    internal IReadOnlyList<string> Suggestions(string query, CancellationToken token)
    {
        try
        {
            return _client.Suggestions(query, token);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>The first window of a channel tab, and a page to draw the rest from.</summary>
    /// <param name="Items">The rows of the first window, empty when the tab holds nothing.</param>
    /// <param name="Page">The page the results window pages the tab through, or null on failure.</param>
    internal readonly record struct ChannelTabResult(
        IReadOnlyList<YouTubeResult> Items, IResultPage? Page, ResolveFailure Failure, string Detail);

    /// <summary>Opens one tab of a channel: builds the page and draws its first window.</summary>
    /// <remarks>
    /// Runs on a worker thread, behind a progress window when it is the tab a channel first opens on, and
    /// straight on the background thread when the user switches tabs. The first window is drawn here rather
    /// than left to the window so a channel that cannot be read fails before anything is shown.
    /// </remarks>
    internal ChannelTabResult OpenChannelTab(string channelBase, string tabKey, CancellationToken token)
    {
        if (!Utils.HasAll)
            return new ChannelTabResult([], null, ResolveFailure.MissingComponents, string.Empty);
        var page = new Client.ChannelPage(_channels, channelBase, tabKey);
        try
        {
            var first = page.Take(Client.ChannelPage.Batch, token).GetAwaiter().GetResult();
            return new ChannelTabResult(first, page, ResolveFailure.None, string.Empty);
        }
        catch (Exception failure)
        {
            var explained = FailureMapping.Explain(failure, token);
            _ = page.DisposeAsync();
            return new ChannelTabResult([], null, explained.Failure, explained.Detail);
        }
    }
}
