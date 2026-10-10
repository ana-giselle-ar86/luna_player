using System.Globalization;
using System.Text.Json;

namespace LunaPlayer.YouTube.Client;

/// <summary>Reads the JSON yt-dlp prints: the small set of helpers the resolver, downloader and channel
/// extractor share for walking yt-dlp's objects and mapping them onto the neutral
/// <see cref="YouTubeResult"/>.</summary>
internal static class Helpers
{
    internal static JsonElement? Parse(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return null;
        try
        {
            using var document = JsonDocument.Parse(string.Join('\n', lines));
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not a failure worth its own path: every caller treats "no data" and "unreadable data" the
            // same, and the message they show says which of their own jobs did not finish.
            return null;
        }
    }

    /// <summary>The address to play, out of everything yt-dlp said about a video.</summary>
    internal static string? PickStream(JsonElement data)
    {
        if (Text(data, "url") is { Length: > 0 } direct)
            return direct;
        // A format that needs joining is reported as parts. This playback path cannot join separate live
        // streams, so use the first part, which carries the picture.
        if (!data.TryGetProperty("requested_formats", out var parts) || parts.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object && Text(part, "url") is { Length: > 0 } address)
                return address;
        }
        return null;
    }

    /// <summary>What yt-dlp says about a video, falling back to what the caller already knew.</summary>
    internal static YouTubeResult Describe(JsonElement data, string watchUrl, YouTubeResult known)
    {
        var found = Entry(data);
        if (found is not YouTubeResult item)
            return known.Url.Length > 0 ? known : known with { Url = watchUrl, Title = watchUrl };
        return known.Url.Length > 0 ? item with { Url = known.Url } : item;
    }

    internal static YouTubeResult? Entry(JsonElement entry)
    {
        var url = Text(entry, "webpage_url") ?? string.Empty;
        var id = Text(entry, "id") ?? string.Empty;
        if (url.Length == 0)
        {
            var raw = Text(entry, "url") ?? string.Empty;
            url = raw.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? raw
                : id.Length > 0
                    ? Utils.WatchUrl(id)
                    : raw.Length > 0 ? Utils.WatchUrl(raw) : string.Empty;
        }
        if (url.Length == 0)
            return null;
        // Spelled the one way the player spells them, so an address from here and one from a search name
        // the same cache entry and the same playlist source.
        url = Utils.Canonical(url) ?? url;
        var duration = entry.TryGetProperty("duration", out var seconds)
            && seconds.ValueKind == JsonValueKind.Number
            && seconds.TryGetDouble(out var value) && value > 0
                ? TimeSpan.FromSeconds(value)
                : (TimeSpan?)null;
        return new YouTubeResult(
            id,
            Text(entry, "title") is { Length: > 0 } title ? title : url,
            Text(entry, "channel") ?? Text(entry, "uploader") ?? string.Empty,
            duration,
            url,
            Text(entry, "channel_url") ?? Text(entry, "uploader_url") ?? string.Empty);
    }

    /// <summary>One row of a channel tab, tagged with what kind of thing it is so the browser knows whether
    /// to play it, page into a playlist or open another channel.</summary>
    /// <remarks>
    /// The tab a row came from settles its kind for the tabs that hold only one - the videos, shorts and
    /// streams tabs are all videos, playlists are playlists, channels are channels. Only the mixed home and
    /// community tabs fall back to reading the row's own shape. A playlist or channel row keeps its own list
    /// or channel address rather than being canonicalised to a watch URL, which would name nothing.
    /// </remarks>
    internal static YouTubeResult? ChannelEntry(JsonElement entry, string tabKey)
    {
        var id = Text(entry, "id") ?? string.Empty;
        var rawUrl = Text(entry, "url") ?? Text(entry, "webpage_url") ?? string.Empty;
        var type = ChannelItemType(entry, tabKey, rawUrl);
        if (type is YouTubeItemType.Video)
            return Entry(entry) is YouTubeResult video ? video with { ItemType = YouTubeItemType.Video } : null;

        var url = type is YouTubeItemType.Playlist ? PlaylistUrl(id, rawUrl) : ChannelUrl(id, rawUrl);
        if (url.Length == 0)
            return null;
        return new YouTubeResult(
            id,
            Text(entry, "title") is { Length: > 0 } title ? title : url,
            Text(entry, "channel") ?? Text(entry, "uploader") ?? string.Empty,
            null,
            url,
            Text(entry, "channel_url") ?? Text(entry, "uploader_url") ?? string.Empty)
        {
            ItemType = type,
        };
    }

    private static YouTubeItemType ChannelItemType(JsonElement entry, string tabKey, string rawUrl)
    {
        // The single-kind tabs answer for every row in them.
        if (tabKey is "videos" or "shorts" or "streams")
            return YouTubeItemType.Video;
        if (tabKey is "playlists")
            return YouTubeItemType.Playlist;
        if (tabKey is "channels")
            return YouTubeItemType.Channel;
        var tag = Text(entry, "_type");
        if (string.Equals(tag, "playlist", StringComparison.OrdinalIgnoreCase)
            || rawUrl.Contains("list=", StringComparison.Ordinal)
            || rawUrl.Contains("/playlist", StringComparison.OrdinalIgnoreCase))
            return YouTubeItemType.Playlist;
        if (string.Equals(tag, "channel", StringComparison.OrdinalIgnoreCase)
            || rawUrl.Contains("/channel/", StringComparison.OrdinalIgnoreCase)
            || rawUrl.Contains("/@", StringComparison.Ordinal))
            return YouTubeItemType.Channel;
        return YouTubeItemType.Video;
    }

    private static string PlaylistUrl(string id, string rawUrl)
    {
        if (rawUrl.Contains("list=", StringComparison.Ordinal)
            || rawUrl.Contains("/playlist", StringComparison.OrdinalIgnoreCase))
            return rawUrl;
        return id.Length > 0 ? $"https://www.youtube.com/playlist?list={id}" : rawUrl;
    }

    private static string ChannelUrl(string id, string rawUrl)
    {
        if (rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return rawUrl;
        return id.Length > 0 ? $"https://www.youtube.com/channel/{id}" : rawUrl;
    }

    internal static string? Text(JsonElement value, string name)
        => value.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()?.Trim()
            : null;

    /// <summary>Reads a number, whether yt-dlp wrote it as a JSON number or as a numeric string.</summary>
    internal static double? Number(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var found))
            return null;
        if (found.ValueKind == JsonValueKind.Number && found.TryGetDouble(out var number))
            return number;
        return found.ValueKind == JsonValueKind.String
            && double.TryParse(found.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    /// <summary>Whether a format names a real codec for a track, rather than yt-dlp's "none".</summary>
    internal static bool Has(JsonElement format, string codec)
        => Text(format, codec) is { Length: > 0 } value
            && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
}
