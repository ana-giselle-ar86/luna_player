using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

internal static class ComponentParser
{
    internal static JsonObject ParseVideo(JsonNode element, string? shelfTitle = null)
    {
        JsonNode? video = Get(element, "videoRenderer") ?? throw new PyYtException("Missing videoRenderer.");
        string? id = String(video, "videoId");
        string? channelId = String(video, "ownerText", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId");
        return Object(
            ("type", Value("video")),
            ("id", Value(id)),
            ("title", Value(String(video, "title", "runs", 0, "text"))),
            ("publishedTime", Value(String(video, "publishedTimeText", "simpleText"))),
            ("duration", Value(String(video, "lengthText", "simpleText"))),
            ("viewCount", Object(
                ("text", Value(String(video, "viewCountText", "simpleText"))),
                ("short", Value(String(video, "shortViewCountText", "simpleText"))))),
            ("thumbnails", Get(video, "thumbnail", "thumbnails")),
            ("richThumbnail", Get(video, "richThumbnail", "movingThumbnailRenderer", "movingThumbnailDetails", "thumbnails", 0)),
            ("descriptionSnippet", Get(video, "detailedMetadataSnippets", 0, "snippetText", "runs")),
            ("channel", Object(
                ("name", Value(String(video, "ownerText", "runs", 0, "text"))),
                ("id", Value(channelId)),
                ("thumbnails", Get(video, "channelThumbnailSupportedRenderers", "channelThumbnailWithLinkRenderer", "thumbnail", "thumbnails")),
                ("link", Value(channelId is null ? null : $"https://www.youtube.com/channel/{channelId}")))),
            ("accessibility", Object(
                ("title", Value(String(video, "title", "accessibility", "accessibilityData", "label"))),
                ("duration", Value(String(video, "lengthText", "accessibility", "accessibilityData", "label"))))),
            ("link", Value(id is null ? null : $"https://www.youtube.com/watch?v={id}")),
            ("shelfTitle", Value(shelfTitle)));
    }

    internal static JsonObject ParseChannel(JsonNode element)
    {
        JsonNode? channel = Get(element, "channelRenderer") ?? throw new PyYtException("Missing channelRenderer.");
        string? id = String(channel, "channelId");
        return Object(
            ("type", Value("channel")), ("id", Value(id)),
            ("title", Value(String(channel, "title", "simpleText"))),
            ("thumbnails", Get(channel, "thumbnail", "thumbnails")),
            ("videoCount", Value(String(channel, "videoCountText", "runs", 0, "text"))),
            ("descriptionSnippet", Get(channel, "descriptionSnippet", "runs")),
            ("subscribers", Value(String(channel, "subscriberCountText", "simpleText"))),
            ("link", Value(id is null ? null : $"https://www.youtube.com/channel/{id}")));
    }

    internal static JsonObject ParsePlaylist(JsonNode element)
    {
        JsonNode playlist;
        string? id;
        string? title;
        string? videoCount;
        string? channelName;
        string? channelId;
        JsonNode? thumbnails;
        if (Get(element, "playlistRenderer") is JsonNode renderer)
        {
            playlist = renderer;
            id = String(playlist, "playlistId");
            title = String(playlist, "title", "simpleText");
            videoCount = String(playlist, "videoCount");
            channelName = String(playlist, "shortBylineText", "runs", 0, "text");
            channelId = String(playlist, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId");
            thumbnails = Get(playlist, "thumbnailRenderer", "playlistVideoThumbnailRenderer", "thumbnail", "thumbnails");
        }
        else if (Get(element, "lockupViewModel") is JsonNode lockup)
        {
            id = String(lockup, "contentId");
            title = String(lockup, "metadata", "lockupMetadataViewModel", "title", "content");
            videoCount = String(lockup, "contentImage", "collectionThumbnailViewModel", "primaryThumbnail", "thumbnailViewModel", "overlays", 0, "thumbnailOverlayBadgeViewModel", "thumbnailBadges", 0, "thumbnailBadgeViewModel", "text");
            channelName = String(lockup, "metadata", "lockupMetadataViewModel", "metadata", "contentMetadataViewModel", "metadataRows", 0, "metadataParts", 0, "text", "content");
            channelId = String(lockup, "metadata", "lockupMetadataViewModel", "metadata", "contentMetadataViewModel", "metadataRows", 0, "metadataParts", 0, "text", "commandRuns", 0, "onTap", "innertubeCommand", "browseEndpoint", "browseId");
            thumbnails = Get(lockup, "contentImage", "collectionThumbnailViewModel", "primaryThumbnail", "thumbnailViewModel", "image", "sources");
        }
        else throw new PyYtException("Unrecognized playlist result.");

        return Object(
            ("type", Value("playlist")), ("id", Value(id)), ("title", Value(title)),
            ("videoCount", Value(videoCount)), ("thumbnails", thumbnails),
            ("channel", Object(("name", Value(channelName)), ("id", Value(channelId)),
                ("link", Value(channelId is null ? null : $"https://www.youtube.com/channel/{channelId}")))),
            ("link", Value(id is null ? null : $"https://www.youtube.com/playlist?list={id}")));
    }
}
