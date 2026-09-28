using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

public sealed class Channel : IDisposable
{
    private readonly YouTubeHttpClient _http;
    private readonly string _channelId;
    private readonly string _requestType;
    private string? _continuation;

    public Channel(string channelId, string requestType = ChannelRequestType.Playlists, TimeSpan? timeout = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        _channelId = channelId;
        _requestType = requestType;
        _http = new YouTubeHttpClient(httpClient, timeout ?? TimeSpan.FromSeconds(7), 2);
    }

    public JsonObject Result { get; private set; } = new();
    public bool HasMorePlaylists => _continuation is not null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        JsonObject body = RequestPayload.Create();
        body["params"] = _requestType;
        body["browseId"] = _channelId;
        JsonNode response = await _http.PostJsonAsync(YouTubeConstants.ApiUrl("browse", false), body, cancellationToken).ConfigureAwait(false);
        ParseInitial(response);
    }

    public async Task NextAsync(CancellationToken cancellationToken = default)
    {
        if (_continuation is null) return;
        JsonObject body = RequestPayload.Create();
        body["continuation"] = _continuation;
        JsonNode response = await _http.PostJsonAsync(YouTubeConstants.ApiUrl("browse", false), body, cancellationToken).ConfigureAwait(false);
        _continuation = null;
        if (Get(response, "onResponseReceivedActions", 0, "appendContinuationItemsAction", "continuationItems") is not JsonArray items) return;
        if (Result["playlists"] is not JsonArray playlists)
        {
            playlists = [];
            Result["playlists"] = playlists;
        }
        foreach (JsonNode? item in items)
        {
            if (Get(item, "continuationItemRenderer") is not null)
            {
                _continuation = String(item, "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
                break;
            }
            if (Get(item, "gridPlaylistRenderer") is not null || Get(item, "lockupViewModel") is not null)
                Add(playlists, PlaylistParse(item!));
        }
    }

    public static async Task<JsonObject> GetAsync(string channelId, string requestType = ChannelRequestType.Playlists,
        TimeSpan? timeout = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        using var channel = new Channel(channelId, requestType, timeout, httpClient);
        await channel.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return (JsonObject)channel.Result.DeepClone();
    }

    private void ParseInitial(JsonNode response)
    {
        var thumbnails = new JsonArray();
        Append(thumbnails, Get(response, "header", "c4TabbedHeaderRenderer", "avatar", "thumbnails") as JsonArray);
        Append(thumbnails, Get(response, "metadata", "channelMetadataRenderer", "avatar", "thumbnails") as JsonArray);
        Append(thumbnails, Get(response, "microformat", "microformatDataRenderer", "thumbnail", "thumbnails") as JsonArray);
        var playlists = new JsonArray();
        JsonNode? aboutTab = null;
        if (Get(response, "contents", "twoColumnBrowseResultsRenderer", "tabs") is JsonArray tabs)
        {
            foreach (JsonNode? tab in tabs)
            {
                string? title = String(tab, "tabRenderer", "title");
                if (title == "About") aboutTab = Get(tab, "tabRenderer");
                if (title != "Playlists") continue;
                JsonNode? itemsNode = Get(tab, "tabRenderer", "content", "sectionListRenderer", "contents", 0,
                    "itemSectionRenderer", "contents", 0, "gridRenderer", "items");
                if (itemsNode is not JsonArray items) continue;
                foreach (JsonNode? item in items)
                {
                    if (Get(item, "continuationItemRenderer") is not null)
                    {
                        _continuation = String(item, "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
                        break;
                    }
                    if (Get(item, "gridPlaylistRenderer") is not null || Get(item, "lockupViewModel") is not null)
                        Add(playlists, PlaylistParse(item!));
                }
            }
        }
        JsonNode? metadata = Get(aboutTab, "content", "sectionListRenderer", "contents", 0, "itemSectionRenderer", "contents", 0,
                "channelAboutFullMetadataRenderer")
            // Fallback for the new About tab structure (py_yt: aboutChannelMetadataViewModel).
            ?? Get(aboutTab, "content", "sectionListRenderer", "contents", 0, "itemSectionRenderer", "contents", 0,
                "aboutChannelRenderer", "metadata", "aboutChannelMetadataViewModel");
        Result = Object(
            ("id", Value(String(response, "metadata", "channelMetadataRenderer", "externalId"))),
            ("url", Value(String(response, "metadata", "channelMetadataRenderer", "channelUrl"))),
            ("description", Value(String(response, "metadata", "channelMetadataRenderer", "description"))),
            ("title", Value(String(response, "metadata", "channelMetadataRenderer", "title"))),
            ("banners", Get(response, "header", "c4TabbedHeaderRenderer", "banner", "thumbnails")),
            ("subscribers", Object(
                ("simpleText", Value(String(response, "header", "c4TabbedHeaderRenderer", "subscriberCountText", "simpleText"))),
                ("label", Value(String(response, "header", "c4TabbedHeaderRenderer", "subscriberCountText", "accessibility", "accessibilityData", "label"))))),
            ("thumbnails", thumbnails),
            ("availableCountryCodes", Get(response, "metadata", "channelMetadataRenderer", "availableCountryCodes")),
            ("isFamilySafe", Get(response, "metadata", "channelMetadataRenderer", "isFamilySafe")),
            ("keywords", Value(String(response, "metadata", "channelMetadataRenderer", "keywords"))),
            ("tags", Get(response, "microformat", "microformatDataRenderer", "tags")),
            ("views", Value(metadata is null ? null
                : String(metadata, "viewCountText", "simpleText") ?? String(metadata, "viewCount"))),
            ("joinedDate", Value(metadata is null ? null
                : String(metadata, "joinedDateText", "runs", -1, "text") ?? String(metadata, "joinedDateText"))),
            ("country", Value(metadata is null ? null : String(metadata, "country", "simpleText"))),
            ("playlists", playlists));
    }

    // Mirrors ChannelCore.playlist_parse: gridPlaylistRenderer, lockupViewModel, else a bare renderer.
    private static JsonObject PlaylistParse(JsonNode item)
    {
        if (Get(item, "gridPlaylistRenderer") is JsonNode grid)
            return Object(
                ("id", Value(String(grid, "playlistId"))), ("thumbnails", Get(grid, "thumbnail", "thumbnails")),
                ("title", Value(String(grid, "title", "runs", 0, "text"))),
                ("videoCount", Value(String(grid, "videoCountShortText", "simpleText"))),
                ("lastEdited", Value(String(grid, "publishedTimeText", "simpleText"))));
        if (Get(item, "lockupViewModel") is JsonNode lockup)
            return Object(
                ("id", Value(String(lockup, "contentId"))),
                ("thumbnails", Get(lockup, "contentImage", "collectionThumbnailViewModel", "primaryThumbnail", "thumbnailViewModel", "image", "sources")),
                ("title", Value(String(lockup, "metadata", "lockupMetadataViewModel", "title", "content"))),
                ("videoCount", Value(String(lockup, "contentImage", "collectionThumbnailViewModel", "primaryThumbnail",
                    "thumbnailViewModel", "overlays", 0, "thumbnailOverlayBadgeViewModel", "thumbnailBadges", 0,
                    "thumbnailBadgeViewModel", "text"))),
                ("lastEdited", Value((string?)null)));
        return Object(
            ("id", Value(String(item, "playlistId"))), ("thumbnails", Get(item, "thumbnail", "thumbnails")),
            ("title", Value(String(item, "title", "runs", 0, "text"))),
            ("videoCount", Value(String(item, "videoCountShortText", "simpleText"))),
            ("lastEdited", Value(String(item, "publishedTimeText", "simpleText"))));
    }

    private static void Append(JsonArray destination, JsonArray? source)
    {
        if (source is null) return;
        foreach (JsonNode? item in source) destination.Add(Copy(item));
    }

    public void Dispose() => _http.Dispose();
}
