using System.Text.Json;

namespace LunaPlayer.YouTube.Client;

/// <summary>The parts of a YouTube channel a person can browse, and the URLs that name them.</summary>
///
/// <remarks>
/// The tab keys are stable and used off the UI thread to shape the address yt-dlp is pointed at; the
/// display names are translated and so are handed out only through <see cref="DisplayNames"/>, which must
/// be called on the UI thread. The "live" tab is spelled "streams" here, which is the path segment YouTube
/// actually serves. The information-only "about" tab is left out, because its rows are neither played nor
/// browsed into.
/// </remarks>
internal static class Tabs
{
    /// <summary>The tab keys, in the order they are shown. Aligned with <see cref="DisplayNames"/>.</summary>
    internal static readonly string[] Keys =
        ["home", "videos", "shorts", "streams", "playlists", "community", "channels"];

    /// <summary>The tab a channel opens on: its videos.</summary>
    internal static int DefaultIndex => Array.IndexOf(Keys, "videos") is var index && index >= 0 ? index : 0;

    /// <summary>The tab keys spelled for the user, in the same order as <see cref="Keys"/>.</summary>
    /// <remarks>Uses <c>Tr</c>, so the UI thread only.</remarks>
    internal static IReadOnlyList<string> DisplayNames() =>
    [
        // Translators: A channel browser tab: the channel's front page.
        Tr("Home"),
        // Translators: A channel browser tab: the channel's ordinary videos.
        Tr("Videos"),
        // Translators: A channel browser tab: the channel's short vertical videos.
        Tr("Shorts"),
        // Translators: A channel browser tab: the channel's live streams and their recordings.
        Tr("Streams"),
        // Translators: A channel browser tab: the playlists the channel publishes.
        Tr("Playlists"),
        // Translators: A channel browser tab: the channel's community posts.
        Tr("Community"),
        // Translators: A channel browser tab: other channels this one features.
        Tr("Channels"),
    ];

    // The trailing path segments that name a tab rather than the channel itself. Stripped so a URL that
    // already points at one tab still yields the bare channel to build the others from.
    private static readonly HashSet<string> TabSegments = new(StringComparer.OrdinalIgnoreCase)
        { "videos", "shorts", "streams", "live", "playlists", "community", "posts", "channels", "featured", "about" };

    /// <summary>The canonical channel address, with any trailing tab segment removed, that the tab URLs are
    /// built from.</summary>
    internal static string Normalise(string channelIdOrUrl)
    {
        var raw = (channelIdOrUrl ?? string.Empty).Trim();
        if (raw.Length == 0)
            return raw;
        // A bare id or handle with no scheme names a channel on its own.
        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            if (raw.StartsWith('@'))
                return $"https://www.youtube.com/{raw}";
            if (raw.StartsWith("UC", StringComparison.Ordinal))
                return $"https://www.youtube.com/channel/{raw}";
            return $"https://www.youtube.com/@{raw}";
        }
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return raw;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && TabSegments.Contains(segments[^1]))
            segments = segments[..^1];
        var path = segments.Length == 0 ? string.Empty : "/" + string.Join('/', segments);
        return $"{uri.Scheme}://{uri.Host}{path}";
    }

    /// <summary>The address of one tab of a channel.</summary>
    internal static string TabUrl(string channelBase, string tabKey)
    {
        var suffix = tabKey switch
        {
            "videos" => "/videos",
            "shorts" => "/shorts",
            "streams" => "/streams",
            "playlists" => "/playlists",
            "community" => "/community",
            "channels" => "/channels",
            // Home is the bare channel page; an unknown key is treated the same rather than guessed at.
            _ => string.Empty,
        };
        return channelBase.TrimEnd('/') + suffix;
    }
}

/// <summary>Lists a channel's tabs through yt-dlp's flat extraction.</summary>
///
/// <remarks>
/// Backed by yt-dlp rather than the metadata library, which is why the channel browser is gated on the
/// programs being installed. Waits for an external process and must run on a worker thread.
/// </remarks>
internal sealed class Channels
{
    private readonly Runner _runner;

    internal Channels(Runner runner) => _runner = runner;

    /// <summary>One window of rows from a channel tab, and how many raw entries that window held.</summary>
    /// <remarks>
    /// <paramref name="start"/> and <paramref name="end"/> are 1-based and inclusive. The raw count is
    /// returned alongside the mapped rows
    /// because the paging decision - whether more windows remain - is about how much the tab held, not how
    /// much of it was playable: a window can be full of community posts that map to nothing and still not be
    /// the last one. A failed run with nothing to say is treated as the end of the tab rather than an error,
    /// because <c>--ignore-errors</c> lets yt-dlp finish a partial tab.
    /// </remarks>
    internal (IReadOnlyList<YouTubeResult> Items, int RawCount) ChannelTab(
        string channelBase, string tabKey, int start, int end, CancellationToken token)
    {
        var url = Tabs.TabUrl(channelBase, tabKey);
        var run = _runner.Run(
            ["--flat-playlist", "--dump-single-json", "--ignore-errors", "-I", $"{start}:{end}", url],
            token);
        if (Helpers.Parse(run.Lines) is not JsonElement data)
        {
            if (run.Failed)
                throw new InvalidOperationException(run.Diagnostic);
            return ([], 0);
        }
        var items = new List<YouTubeResult>();
        var raw = 0;
        if (data.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;
                raw++;
                if (Helpers.ChannelEntry(entry, tabKey) is YouTubeResult found)
                    items.Add(found);
            }
        }
        return (items, raw);
    }
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
internal sealed class ChannelPage : IResultPage, IAsyncDisposable
{
    internal const int Batch = 30;

    private readonly Channels _channels;
    private readonly string _channelBase;
    private readonly string _tabKey;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private int _next = 1;
    private bool _exhausted;

    internal ChannelPage(Channels channels, string channelBase, string tabKey)
    {
        _channels = channels;
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
                var (items, raw) = _channels.ChannelTab(_channelBase, _tabKey, start, end, token);
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
