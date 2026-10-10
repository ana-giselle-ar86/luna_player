using System.Globalization;
using System.Text.Json.Nodes;

namespace LunaPlayer.YouTube.Metadata;

/// <summary>Maps the JSON trees PyYt returns onto the neutral <see cref="YouTubeResult"/> the rest of the
/// player speaks in. PyYt keeps its own navigation helpers to itself, so this walks the trees with the
/// small <see cref="Str"/> navigator of its own.</summary>
internal static class Mapping
{
    /// <summary>Maps one search-result object to a neutral row, dispatching on the kind PyYt tagged it with.
    /// A video from inside a playlist carries no tag and falls to the video mapping, which is what it is.</summary>
    internal static YouTubeResult? ToResult(JsonObject item) => Str(item, "type") switch
    {
        "playlist" => PlaylistResult(item),
        "channel" => ChannelResult(item),
        _ => VideoResult(item),
    };

    private static YouTubeResult? VideoResult(JsonObject item)
    {
        string? id = Str(item, "id");
        string url = id is not null ? Utils.WatchUrl(id) : Str(item, "link") ?? string.Empty;
        if (url.Length == 0)
            return null;
        return new YouTubeResult(
            id ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            Str(item, "channel", "name") ?? string.Empty,
            ParseDuration(Str(item, "duration")),
            url,
            Str(item, "channel", "link") ?? string.Empty)
        {
            Views = Str(item, "viewCount", "text"),
            PublishedTime = Str(item, "publishedTime"),
            ItemType = YouTubeItemType.Video,
        };
    }

    private static YouTubeResult PlaylistResult(JsonObject item)
    {
        string? id = Str(item, "id");
        string url = Str(item, "link")
            ?? (id is null ? string.Empty : $"https://www.youtube.com/playlist?list={id}");
        return new YouTubeResult(
            id ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            Str(item, "channel", "name") ?? string.Empty,
            null,
            url,
            Str(item, "channel", "link") ?? string.Empty)
        {
            // The count of videos rides in Views: it is the one secondary line a playlist row has to show.
            Views = Str(item, "videoCount"),
            ItemType = YouTubeItemType.Playlist,
        };
    }

    private static YouTubeResult ChannelResult(JsonObject item)
    {
        string? id = Str(item, "id");
        string url = Str(item, "link")
            ?? (id is null ? string.Empty : $"https://www.youtube.com/channel/{id}");
        return new YouTubeResult(
            id ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            null,
            url,
            url)
        {
            Views = Str(item, "subscribers"),
            PublishedTime = Str(item, "videoCount"),
            ItemType = YouTubeItemType.Channel,
        };
    }

    /// <summary>Reads a string that lives at the end of a path of object keys, or null if any step is missing.
    /// PyYt keeps its own navigator internal, so this is the small piece of it the mappings here need.</summary>
    internal static string? Str(JsonNode? node, params string[] keys)
    {
        JsonNode? current = node;
        foreach (string key in keys)
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(key, out current) || current is null)
                return null;
        }
        if (current is JsonValue value && value.TryGetValue(out string? text))
            return text;
        return current?.ToString();
    }

    /// <summary>Reads a duration written either as whole seconds or as "mm:ss"/"h:mm:ss"; null for a live
    /// stream, which states no length.</summary>
    private static TimeSpan? ParseDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim();
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
            return TimeSpan.FromSeconds(seconds);
        string[] parts = text.Split(':');
        if (parts.Length is < 2 or > 3)
            return null;
        try
        {
            int hours = parts.Length == 3 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
            int minutes = int.Parse(parts[^2], CultureInfo.InvariantCulture);
            int secs = int.Parse(parts[^1], CultureInfo.InvariantCulture);
            return new TimeSpan(hours, minutes, secs);
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            return null;
        }
    }
}
