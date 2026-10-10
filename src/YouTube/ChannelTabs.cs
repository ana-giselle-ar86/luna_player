namespace LunaPlayer.YouTube;

/// <summary>The parts of a YouTube channel a person can browse, and the URLs that name them.</summary>
///
/// <remarks>
/// The tab keys are stable and used off the UI thread to shape the address yt-dlp is pointed at; the
/// display names are translated and so are handed out only through <see cref="DisplayNames"/>, which must
/// be called on the UI thread. The "live" tab is spelled "streams" here, which is the path segment YouTube
/// actually serves. The information-only "about" tab is left out, because its rows are neither played nor
/// browsed into.
/// </remarks>
internal static class ChannelTabs
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
