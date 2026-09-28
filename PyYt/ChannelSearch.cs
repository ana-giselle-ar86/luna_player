using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

public sealed class ChannelSearch : IDisposable
{
    private readonly YouTubeHttpClient _http;
    private readonly string _query;
    private string? _browseId;
    private readonly string _language;
    private readonly string _region;
    private readonly string _searchPreferences;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly HttpClient? _httpClient;

    public ChannelSearch(string query, string? browseId = null, string language = "en", string region = "US",
        string searchPreferences = "EgZzZWFyY2jyBgQKAloA", TimeSpan? timeout = null, int maxRetries = 2, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        _query = query;
        _browseId = string.IsNullOrWhiteSpace(browseId) ? null : ExtractChannelId(browseId);
        _language = language;
        _region = region;
        _searchPreferences = searchPreferences;
        _timeout = timeout ?? TimeSpan.FromSeconds(7);
        _maxRetries = maxRetries;
        _httpClient = httpClient;
        _http = new YouTubeHttpClient(httpClient, _timeout, maxRetries);
    }

    // Mirrors ChannelSearchCore._extract_channel_id: pull the id out of a channel URL, else pass through.
    private static string ExtractChannelId(string browseIdOrUrl)
    {
        string clean = browseIdOrUrl.Trim();
        const string marker = "youtube.com/channel/";
        int idx = clean.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0) return clean[(idx + marker.Length)..].Split('/')[0].Split('?')[0];
        return clean;
    }

    public async Task<JsonObject> NextAsync(CancellationToken cancellationToken = default)
    {
        if (_browseId is null) await ResolveBrowseIdAsync(cancellationToken).ConfigureAwait(false);
        if (_browseId is null) return new JsonObject { ["result"] = new JsonArray() };

        JsonObject body = RequestPayload.Build(
            language: _language,
            region: _region,
            @params: _searchPreferences,
            extraFields: new (string, JsonNode?)[]
            {
                ("query", JsonValue.Create(_query)),
                ("browseId", JsonValue.Create(_browseId)),
            });
        JsonNode response = await _http.PostJsonAsync(YouTubeConstants.ApiUrl("browse"), body, cancellationToken).ConfigureAwait(false);
        JsonArray source = ExtractSource(response);
        var results = new JsonArray();
        foreach (JsonNode? item in source)
        {
            if (item is not JsonObject element || element.ContainsKey("continuationItemRenderer")) continue;
            if (Get(element, "gridPlaylistRenderer") is JsonNode grid)
                Add(results, ParseGridPlaylist(grid));
            else if (Get(element, "itemSectionRenderer", "contents", 0, "videoRenderer") is JsonNode video)
                Add(results, ParseVideo(video));
            else if (Get(element, "itemSectionRenderer", "contents", 0, "playlistRenderer") is JsonNode playlist)
                Add(results, ParsePlaylist(playlist));
        }
        return new JsonObject { ["result"] = results };
    }

    // Mirrors ChannelSearchCore._resolve_browse_id: a limit-1 channels search yields the channel id.
    private async Task ResolveBrowseIdAsync(CancellationToken cancellationToken)
    {
        using var search = new ChannelsSearch(_query, limit: 1, language: _language, region: _region,
            timeout: _timeout, maxRetries: _maxRetries, httpClient: _httpClient);
        JsonObject result = await search.NextAsync(cancellationToken).ConfigureAwait(false);
        if (result["result"] is JsonArray results && results.Count > 0 &&
            results[0] is JsonObject first && first["id"] is JsonValue idValue &&
            idValue.TryGetValue<string>(out string? id) && !string.IsNullOrEmpty(id))
        {
            _browseId = id;
        }
    }

    private static JsonArray ExtractSource(JsonNode response)
    {
        if (Get(response, "contents", "twoColumnBrowseResultsRenderer", "tabs") is not JsonArray tabs || tabs.Count == 0)
            throw new PyYtException("Could not parse the channel search response.");
        JsonNode? last = tabs[^1];
        return (Get(last, "expandableTabRenderer", "content", "sectionListRenderer", "contents")
            ?? Get(last, "tabRenderer", "content", "sectionListRenderer", "contents")) as JsonArray ?? [];
    }

    private static JsonObject ParseVideo(JsonNode video) => Object(
        ("id", Value(String(video, "videoId"))),
        ("thumbnails", Object(
            ("normal", Get(video, "thumbnail", "thumbnails")),
            ("rich", Get(video, "richThumbnail", "movingThumbnailRenderer", "movingThumbnailDetails", "thumbnails")))),
        ("title", Value(String(video, "title", "runs", 0, "text"))),
        ("descriptionSnippet", Value(String(video, "descriptionSnippet", "runs", 0, "text"))),
        ("uri", Value(String(video, "navigationEndpoint", "commandMetadata", "webCommandMetadata", "url"))),
        ("views", Object(
            ("precise", Value(String(video, "viewCountText", "simpleText"))),
            ("simple", Value(String(video, "shortViewCountText", "simpleText"))),
            ("approximate", Value(String(video, "shortViewCountText", "accessibility", "accessibilityData", "label"))))),
        ("duration", Object(
            ("simpleText", Value(String(video, "lengthText", "simpleText"))),
            ("text", Value(String(video, "lengthText", "accessibility", "accessibilityData", "label"))))),
        ("published", Value(String(video, "publishedTimeText", "simpleText"))),
        ("channel", Object(
            ("name", Value(String(video, "ownerText", "runs", 0, "text"))),
            ("thumbnails", Get(video, "channelThumbnailSupportedRenderers", "channelThumbnailWithLinkRenderer", "thumbnail", "thumbnails")))),
        ("type", Value("video")));

    private static JsonObject ParsePlaylist(JsonNode playlist)
    {
        var videos = new JsonArray();
        if (Get(playlist, "videos") is JsonArray videoItems)
        {
            foreach (JsonNode? item in videoItems)
            {
                JsonNode? child = Get(item, "childVideoRenderer");
                if (child is null) continue;
                Add(videos, Object(
                    ("id", Value(String(child, "videoId"))), ("title", Value(String(child, "title", "simpleText"))),
                    ("uri", Value(String(child, "navigationEndpoint", "commandMetadata", "webCommandMetadata", "url"))),
                    ("duration", Object(
                        ("simpleText", Value(String(child, "lengthText", "simpleText"))),
                        ("text", Value(String(child, "lengthText", "accessibility", "accessibilityData", "label")))))));
            }
        }
        return Object(
            ("id", Value(String(playlist, "playlistId"))), ("videos", videos),
            ("thumbnails", Object(("normal", Get(playlist, "thumbnails")))),
            ("title", Value(String(playlist, "title", "simpleText"))),
            ("uri", Value(String(playlist, "navigationEndpoint", "commandMetadata", "webCommandMetadata", "url"))),
            ("channel", Object(("name", Value(String(playlist, "longBylineText", "runs", 0, "text"))))),
            ("type", Value("playlist")));
    }

    private static JsonObject ParseGridPlaylist(JsonNode playlist) => Object(
        ("id", Value(String(playlist, "playlistId"))),
        ("thumbnails", Object(("normal", Get(playlist, "thumbnail", "thumbnails", 0)))),
        ("title", Value(String(playlist, "title", "runs", 0, "text"))),
        ("uri", Value(String(playlist, "navigationEndpoint", "commandMetadata", "webCommandMetadata", "url"))),
        ("type", Value("playlist")));

    public void Dispose() => _http.Dispose();
}
