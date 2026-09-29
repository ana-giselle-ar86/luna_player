using LunaPlayer.Configuration;
using LunaPlayer.Media;

namespace LunaPlayer.YouTube;

/// <summary>The operations on a single video that need no session behind them.</summary>
///
/// <remarks>
/// Playing a list of videos lives in <see cref="YouTubeSessions"/>, because it needs the player and the
/// order the results window showed. What is left here is the work that answers about one address and then
/// finishes: saving it, and reading what the uploader wrote under it. Both run on a worker thread behind a
/// progress window, so both report a raw failure rather than a translated one.
///
/// Reading about a video - its description, or the contents of a playlist - goes through PyYt, which needs
/// nothing installed. Saving one goes through yt-dlp, which the user has to fetch first and which is the
/// only thing that can turn a video into a playable file; when the programs it needs are not there, the
/// user is told so rather than quietly getting nothing.
/// </remarks>
internal sealed class Backend
{
    private readonly PyYtClient _client;
    private readonly YtDlpClient _ytDlp;

    internal Backend(PyYtClient client, YtDlpClient ytDlp)
    {
        _client = client;
        _ytDlp = ytDlp;
    }

    /// <summary>Whether the programs yt-dlp needs have been fetched.</summary>
    internal static bool HasComponents => Tools.HasAll;

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
            var explained = PyYtClient.Explain(failure, token);
            return (null, explained.Failure, explained.Detail);
        }
    }

    /// <summary>Every video in a playlist, and what the playlist is called.</summary>
    /// <remarks>
    /// Through PyYt, which reads the listing without needing anything installed - the same as a search, and
    /// the same library. Only turning a video into a playable file needs yt-dlp.
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
            var explained = PyYtClient.Explain(failure, token);
            return (string.Empty, [], explained.Failure, explained.Detail);
        }
    }

    /// <summary>The words YouTube offers to finish what the user has typed, for the search box's live
    /// suggestions.</summary>
    /// <remarks>
    /// Through PyYt, needing nothing installed. Called on the background thread the search dialog starts on
    /// each keystroke; a failed or empty fetch is simply no suggestions, so anything that goes wrong reads as
    /// an empty list rather than an error the box has no way to show.
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

    /// <summary>Saves a video into <paramref name="folder"/>, naming the file after the video.</summary>
    /// <param name="exactQuality">The picture height or audio bitrate the user chose from what the video
    /// offers, or null to save at the quality set in preferences.</param>
    internal YouTubeOutcome Download(
        string watchUrl,
        string folder,
        bool audioOnly,
        int quality,
        Action<ProgressUpdate> report,
        CancellationToken token,
        int? exactQuality = null)
    {
        try
        {
            if (!Tools.HasAll)
                return new YouTubeOutcome(false, MissingComponents);
            _ytDlp.Download(watchUrl, folder, audioOnly, quality,
                (name, got, size) => report(Bytes(name, got, size)), token, exactQuality);
            return YouTubeOutcome.Ok;
        }
        catch (OperationCanceledException)
        {
            // The user's own doing, and the progress window has already gone. Nothing to report.
            return YouTubeOutcome.Ok;
        }
        catch (Exception failure)
        {
            return new YouTubeOutcome(false, failure.Message);
        }
    }

    /// <summary>The distinct qualities a video offers, best first, for the download picker.</summary>
    /// <remarks>
    /// Runs on a worker thread. Anything that goes wrong reading the video's formats reads as an empty list,
    /// so the caller simply falls back to the settings quality rather than a picker it cannot fill - except a
    /// cancellation, which is the user's doing and must travel on so the progress window closes without a
    /// picker appearing.
    /// </remarks>
    internal IReadOnlyList<int> AvailableQualities(string watchUrl, bool audioOnly, CancellationToken token)
    {
        if (!Tools.HasAll)
            return [];
        try
        {
            return _ytDlp.AvailableQualities(watchUrl, audioOnly, token);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return [];
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
        if (!Tools.HasAll)
            return new ChannelTabResult([], null, ResolveFailure.MissingComponents, string.Empty);
        var page = new ChannelTabPage(_ytDlp, channelBase, tabKey);
        try
        {
            var first = page.Take(ChannelTabPage.Batch, token).GetAwaiter().GetResult();
            return new ChannelTabResult(first, page, ResolveFailure.None, string.Empty);
        }
        catch (Exception failure)
        {
            var explained = PyYtClient.Explain(failure, token);
            _ = page.DisposeAsync();
            return new ChannelTabResult([], null, explained.Failure, explained.Detail);
        }
    }

    /// <summary>What the user is told when the programs YouTube playback needs are not installed.</summary>
    /// <remarks>
    /// A property rather than a constant, so it is read at the moment it is needed. <c>Tr</c> may only be
    /// called on the UI thread, and a static initialiser would run wherever this type is first touched -
    /// which, for a download, is on a worker.
    /// </remarks>
    internal static string MissingComponents =>
        // Translators: Shown when something needs the extra programs for yt-dlp and they are not installed.
        Tr("YouTube components are missing.");

    /// <summary>One progress report from a download.</summary>
    /// <remarks>
    /// Bytes where the source knows them and hundredths where it does not, because the window shows the two
    /// sizes as well as the bar and a proportion cannot be turned back into them. yt-dlp states the total
    /// for most videos; a download whose total it did not state reads as unknown, which is honest.
    /// </remarks>
    private static ProgressUpdate Bytes(string name, long got, long size)
        => new((int)Math.Min(got, int.MaxValue), (int)Math.Min(size, int.MaxValue), name);
}
