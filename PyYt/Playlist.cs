using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static PyYt.JsonNavigator;

namespace PyYt;

public sealed partial class Playlist : IDisposable
{
    // Mirrors PlaylistCore.prepare_first_request regexes (applied to the rstrip('/') URL).
    [GeneratedRegex(@"(?<=list=)([a-zA-Z0-9+/=_-]+)")] private static partial Regex ListIdPattern();
    [GeneratedRegex(@"(?<=v=)([a-zA-Z0-9_-]+)")] private static partial Regex VideoIdPattern();

    private readonly string _playlistLink;
    private readonly YouTubeHttpClient _http;
    private string? _continuation;
    private bool _initialized;

    public Playlist(string playlistLink, TimeSpan? timeout = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistLink);
        _playlistLink = playlistLink;
        _http = new YouTubeHttpClient(httpClient, timeout ?? TimeSpan.FromSeconds(2), 2);
    }

    public JsonObject? Info { get; private set; }
    public JsonArray Videos { get; } = [];
    public bool HasMoreVideos => !_initialized || _continuation is not null;

    public async Task GetNextVideosAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            JsonObject result = await LoadFirstAsync(_http, _playlistLink, cancellationToken).ConfigureAwait(false);
            Info = (JsonObject?)Copy(result["info"]);
            Append(Videos, result["videos"] as JsonArray);
            _continuation = String(result, "_continuation");
            _initialized = true;
            return;
        }
        if (_continuation is null) return;
        JsonObject body = RequestPayload.Create();
        body["continuation"] = _continuation;
        JsonNode response = await _http.PostJsonAsync(YouTubeConstants.ApiUrl("browse"), body, cancellationToken).ConfigureAwait(false);
        _continuation = null;
        if (Get(response, "onResponseReceivedActions", 0, "appendContinuationItemsAction", "continuationItems") is not JsonArray items)
            return;
        foreach (JsonNode? item in items)
        {
            if (Get(item, "playlistVideoRenderer") is JsonNode renderer) Add(Videos, ParseVideo(renderer));
            string? token = String(item, "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
            if (token is not null) _continuation = token;
        }
    }

    public static async Task<JsonObject> GetAsync(string playlistLink, TimeSpan? timeout = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        using var http = new YouTubeHttpClient(httpClient, timeout ?? TimeSpan.FromSeconds(2), 2);
        JsonObject result = await LoadFirstAsync(http, playlistLink, cancellationToken).ConfigureAwait(false);
        result.Remove("_continuation");
        return result;
    }

    public static async Task<JsonObject> GetInfoAsync(string playlistLink, TimeSpan? timeout = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        JsonObject result = await GetAsync(playlistLink, timeout, httpClient, cancellationToken).ConfigureAwait(false);
        return (JsonObject?)result["info"]?.DeepClone() ?? new JsonObject();
    }

    public static async Task<JsonObject> GetVideosAsync(string playlistLink, TimeSpan? timeout = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        JsonObject result = await GetAsync(playlistLink, timeout, httpClient, cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["videos"] = result["videos"]?.DeepClone() ?? new JsonArray() };
    }

    private static async Task<JsonObject> LoadFirstAsync(YouTubeHttpClient http, string playlistLink, CancellationToken cancellationToken)
    {
        string clean = (playlistLink ?? string.Empty).TrimEnd('/');
        string id = ExtractPlaylistId(clean);
        if (id.StartsWith("RD", StringComparison.Ordinal))
        {
            // YouTube Mix playlist: query the "next" endpoint with playlistId + videoId.
            Match videoMatch = VideoIdPattern().Match(clean);
            string? videoId = videoMatch.Success ? videoMatch.Value : null;
            JsonObject mixBody = RequestPayload.Build("en", "US", "WEB", YouTubeConstants.ClientVersion, null, null,
                ("playlistId", JsonValue.Create(id)),
                ("videoId", videoId is null ? null : JsonValue.Create(videoId)));
            JsonNode mixResponse = await http.PostJsonAsync(YouTubeConstants.ApiUrl("next"), mixBody, cancellationToken).ConfigureAwait(false);
            return ParseMix(mixResponse);
        }

        JsonObject body = RequestPayload.Create();
        body["browseId"] = id.StartsWith("VL", StringComparison.Ordinal) ? id : "VL" + id;
        JsonNode response = await http.PostJsonAsync(YouTubeConstants.ApiUrl("browse"), body, cancellationToken).ConfigureAwait(false);
        return ParseFirst(response);
    }

    // Mirrors PlaylistCore._get_components "YouTube Mix Playlist (next endpoint)" branch.
    private static JsonObject ParseMix(JsonNode response)
    {
        JsonNode? playlist = Get(response, "contents", "twoColumnWatchNextResults", "playlist", "playlist");
        var videos = new JsonArray();
        if (Get(playlist, "contents") is JsonArray items)
            foreach (JsonNode? video in items)
                if (Get(video, "playlistPanelVideoRenderer") is JsonNode renderer)
                    Add(videos, ParseMixVideo(renderer));
        string? id = String(playlist, "playlistId");
        var info = Object(
            ("id", Value(id)),
            ("title", Value(String(playlist, "title"))),
            ("videoCount", Value(videos.Count.ToString(CultureInfo.InvariantCulture))),
            ("link", Value(id is null ? null : "https://www.youtube.com/playlist?list=" + id)),
            ("channel", null));
        return new JsonObject { ["info"] = info, ["videos"] = videos, ["_continuation"] = null };
    }

    private static JsonObject ParseMixVideo(JsonNode video)
    {
        string? vid = String(video, "videoId");
        string? ownerPath = String(video, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "canonicalBaseUrl");
        string? title = String(video, "title", "simpleText") ?? String(video, "title", "runs", 0, "text");
        return Object(
            ("id", Value(vid)),
            ("thumbnails", Get(video, "thumbnail", "thumbnails")),
            ("title", Value(title)),
            ("channel", Object(
                ("name", Value(String(video, "shortBylineText", "runs", 0, "text"))),
                ("id", Value(String(video, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId"))),
                ("link", Value(ownerPath is null ? null : "https://www.youtube.com" + ownerPath)))),
            ("duration", Value(String(video, "lengthText", "simpleText"))),
            ("link", Value(vid is null ? null : "https://www.youtube.com/watch?v=" + vid)));
    }

    private static JsonObject ParseFirst(JsonNode response)
    {
        if (Get(response, "sidebar", "playlistSidebarRenderer", "items") is not JsonArray sidebar || sidebar.Count == 0)
            throw new PyYtException("Could not find playlist metadata in the YouTube response.");
        JsonNode? primary = Get(sidebar, 0, "playlistSidebarPrimaryInfoRenderer");
        JsonNode? owner = sidebar.Count > 1 ? Get(sidebar, 1, "playlistSidebarSecondaryInfoRenderer", "videoOwner", "videoOwnerRenderer") : null;
        JsonArray rendererItems = FindFirstArray(response, "playlistVideoListRenderer", "contents") ?? [];
        var videos = new JsonArray();
        string? continuation = null;
        foreach (JsonNode? item in rendererItems)
        {
            if (Get(item, "playlistVideoRenderer") is JsonNode renderer) Add(videos, ParseVideo(renderer));
            else if (Get(item, "lockupViewModel") is JsonNode lockup) Add(videos, ParseLockupVideo(lockup));
            continuation ??= String(item, "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
        }
        bool hasOwner = owner is not null;
        string? canonicalOwnerUrl = String(owner, "title", "runs", 0, "navigationEndpoint", "browseEndpoint", "canonicalBaseUrl");
        var info = Object(
            ("id", Value(String(primary, "title", "runs", 0, "navigationEndpoint", "watchEndpoint", "playlistId"))),
            ("thumbnails", Get(primary, "thumbnailRenderer", "playlistVideoThumbnailRenderer", "thumbnail", "thumbnails")
                ?? Get(primary, "thumbnailRenderer", "playlistCustomThumbnailRenderer", "thumbnail", "thumbnails")),
            ("title", Value(String(primary, "title", "runs", 0, "text"))),
            ("videoCount", Value(String(primary, "stats", 0, "runs", 0, "text"))),
            ("viewCount", Value(String(primary, "stats", 1, "simpleText"))),
            ("link", Value(String(response, "microformat", "microformatDataRenderer", "urlCanonical"))),
            ("channel", Object(
                ("id", Value(String(owner, "title", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId"))),
                ("name", Value(String(owner, "title", "runs", 0, "text"))),
                ("detailsAvailable", Value(hasOwner)),
                ("link", Value(canonicalOwnerUrl is null ? null : "https://www.youtube.com" + canonicalOwnerUrl)),
                ("thumbnails", Get(owner, "thumbnail", "thumbnails")))));
        return new JsonObject { ["info"] = info, ["videos"] = videos, ["_continuation"] = continuation };
    }

    private static JsonObject ParseVideo(JsonNode video)
    {
        string? path = String(video, "navigationEndpoint", "commandMetadata", "webCommandMetadata", "url");
        string? ownerPath = String(video, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "canonicalBaseUrl");
        return Object(
            ("id", Value(String(video, "videoId"))), ("thumbnails", Get(video, "thumbnail", "thumbnails")),
            ("title", Value(String(video, "title", "runs", 0, "text"))),
            ("channel", Object(
                ("name", Value(String(video, "shortBylineText", "runs", 0, "text"))),
                ("id", Value(String(video, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId"))),
                ("link", Value(ownerPath is null ? null : "https://www.youtube.com" + ownerPath)))),
            ("duration", Value(String(video, "lengthText", "simpleText"))),
            ("accessibility", Object(
                ("title", Value(String(video, "title", "accessibility", "accessibilityData", "label"))),
                ("duration", Value(String(video, "lengthText", "accessibility", "accessibilityData", "label"))))),
            ("link", Value(path is null ? null : "https://www.youtube.com" + path)),
            ("isPlayable", Get(video, "isPlayable")));
    }

    // Mirrors the "lockupViewModel" branch of PlaylistCore._get_components (video lockups).
    private static JsonObject ParseLockupVideo(JsonNode lockup)
    {
        string? vid = String(lockup, "contentId");
        string? channelName = String(lockup, "metadata", "lockupMetadataViewModel", "metadata",
            "contentMetadataViewModel", "metadataRows", 0, "metadataParts", 0, "text", "content");
        string? channelId = String(lockup, "metadata", "lockupMetadataViewModel", "metadata",
            "contentMetadataViewModel", "metadataRows", 0, "metadataParts", 0, "text", "commandRuns", 0,
            "onTap", "innertubeCommand", "browseEndpoint", "browseId");
        string? duration = null;
        if (Get(lockup, "contentImage", "thumbnailViewModel", "overlays") is JsonArray overlays)
            foreach (JsonNode? overlay in overlays)
            {
                string? badge = String(overlay, "thumbnailBottomOverlayViewModel", "badges", 0, "thumbnailBadgeViewModel", "text");
                if (!string.IsNullOrEmpty(badge)) { duration = badge; break; }
            }
        return Object(
            ("id", Value(vid)),
            ("thumbnails", Get(lockup, "contentImage", "thumbnailViewModel", "image", "sources")),
            ("title", Value(String(lockup, "metadata", "lockupMetadataViewModel", "title", "content"))),
            ("channel", Object(
                ("name", Value(channelName)), ("id", Value(channelId)),
                ("link", Value(channelId is null ? null : $"https://www.youtube.com/channel/{channelId}")))),
            ("duration", Value(duration)),
            ("accessibility", Object(
                ("title", Value(String(lockup, "rendererContext", "accessibilityContext", "label"))),
                ("duration", Value((string?)null)))),
            ("link", Value(vid is null ? null : $"https://www.youtube.com/watch?v={vid}")),
            ("isPlayable", Value(true)));
    }

    private static JsonArray? FindFirstArray(JsonNode? node, string parentName, string arrayName)
    {
        if (node is JsonObject obj)
        {
            if (obj[parentName] is JsonObject parent && parent[arrayName] is JsonArray found) return found;
            foreach ((_, JsonNode? child) in obj)
                if (FindFirstArray(child, parentName, arrayName) is JsonArray nested) return nested;
        }
        else if (node is JsonArray array)
            foreach (JsonNode? child in array)
                if (FindFirstArray(child, parentName, arrayName) is JsonArray nested) return nested;
        return null;
    }

    // Mirrors PlaylistCore.prepare_first_request id extraction: the list= id, else the whole cleaned URL.
    private static string ExtractPlaylistId(string cleanUrl)
    {
        Match match = ListIdPattern().Match(cleanUrl);
        return match.Success ? match.Value : cleanUrl;
    }

    private static void Append(JsonArray destination, JsonArray? source)
    {
        if (source is null) return;
        foreach (JsonNode? item in source) destination.Add(Copy(item));
    }

    public void Dispose() => _http.Dispose();
}
