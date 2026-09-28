using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

/// <summary>
/// Mirrors py_yt's core/recommendations.py RelatedVideosCore: the InnerTube "next" endpoint
/// driver behind Recommendations.get_related. MWEB client (v2.20260821.00.00), no retries,
/// 20s timeout. First page reads the watch-next secondary results; continuation pages read
/// appendContinuationItemsAction items.
/// </summary>
internal sealed class RelatedVideos
{
    private readonly string _videoLink;
    private readonly int _limit;
    private readonly string _language;
    private readonly string _region;
    private readonly TimeSpan _timeout;
    private readonly HttpClient? _httpClient;
    private string? _continuation;

    internal RelatedVideos(string videoLink, int limit, string language, string region, TimeSpan timeout, HttpClient? httpClient)
    {
        _videoLink = videoLink;
        _limit = limit;
        _language = language;
        _region = region;
        _timeout = timeout;
        _httpClient = httpClient;
    }

    internal async Task<JsonObject> NextAsync(CancellationToken cancellationToken)
    {
        var components = new JsonArray();
        using var http = new YouTubeHttpClient(_httpClient, _timeout, 0);
        JsonObject body = RequestPayload.Build(_language, _region, "MWEB", "2.20260821.00.00",
            _continuation, null, ("videoId", JsonValue.Create(GetVideoId(_videoLink))));
        JsonNode response = await http.PostJsonAsync(YouTubeConstants.ApiUrl("next"), body, cancellationToken)
            .ConfigureAwait(false);
        ParseSource(response, components);
        return new JsonObject { ["result"] = components };
    }
    // Mirrors RelatedVideosCore._parse_source.
    private void ParseSource(JsonNode? response, JsonArray components)
    {
        if (response is null) return;
        var contents = new JsonArray();
        if (string.IsNullOrEmpty(_continuation))
        {
            JsonNode? secondary =
                Get(response, "contents", "twoColumnWatchNextResults", "secondaryResults", "secondaryResults", "results")
                ?? Get(response, "contents", "singleColumnWatchNextResults", "pivot", "pivotRenderer", "contents")
                ?? Get(response, "contents", "singleColumnWatchNextResults", "results", "results", "contents");
            if (secondary is JsonArray list)
                foreach (JsonNode? el in list) Add(contents, el?.DeepClone());
        }
        else if (Get(response, "onResponseReceivedEndpoints") is JsonArray endpoints)
        {
            foreach (JsonNode? action in endpoints)
                if (Get(action, "appendContinuationItemsAction", "continuationItems") is JsonArray items)
                    foreach (JsonNode? el in items) Add(contents, el?.DeepClone());
        }

        if (contents.Count == 0) return;
        foreach (JsonNode? element in contents)
        {
            if (element is not JsonObject) { continue; }
            if (Get(element, "compactVideoRenderer") is not null) Add(components, ParseCompactVideo(element!));
            else if (Get(element, "videoWithContextRenderer") is not null) Add(components, ParseVideoWithContext(element!));
            else if (Get(element, "compactPlaylistRenderer") is not null) Add(components, ParseCompactPlaylist(element!));
            else if (Get(element, "itemSectionRenderer") is not null)
            {
                if (Get(element, "itemSectionRenderer", "contents") is JsonArray nested)
                    foreach (JsonNode? item in nested)
                    {
                        if (item is not JsonObject) continue;
                        if (components.Count >= _limit) break;
                        if (Get(item, "compactVideoRenderer") is not null) Add(components, ParseCompactVideo(item));
                        else if (Get(item, "videoWithContextRenderer") is not null) Add(components, ParseVideoWithContext(item));
                    }
            }
            else if (Get(element, "continuationItemRenderer") is not null)
            {
                string? token = String(element, "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
                _continuation = string.IsNullOrEmpty(token) ? null : token;
            }

            if (components.Count >= _limit) break;
        }
    }
    // _get_compact_video_component: title from title.simpleText.
    private static JsonObject ParseCompactVideo(JsonNode element)
    {
        JsonNode? video = Get(element, "compactVideoRenderer");
        string? vid = String(video, "videoId");
        string? cid = String(video, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId");
        return Object(
            ("type", Value("video")), ("id", Value(vid)),
            ("title", Value(String(video, "title", "simpleText"))),
            ("publishedTime", Value(String(video, "publishedTimeText", "simpleText"))),
            ("duration", Value(String(video, "lengthText", "simpleText"))),
            ("viewCount", Object(
                ("text", Value(String(video, "viewCountText", "simpleText"))),
                ("short", Value(String(video, "shortViewCountText", "simpleText"))))),
            ("thumbnails", Get(video, "thumbnail", "thumbnails")),
            ("channel", Object(
                ("name", Value(String(video, "shortBylineText", "runs", 0, "text"))),
                ("id", Value(cid)),
                ("link", Value(cid is null ? null : $"https://www.youtube.com/channel/{cid}")))),
            ("accessibility", Object(
                ("title", Value(String(video, "title", "accessibility", "accessibilityData", "label"))),
                ("duration", Value(String(video, "lengthText", "accessibility", "accessibilityData", "label"))))),
            ("link", Value(vid is null ? null : $"https://www.youtube.com/watch?v={vid}")));
    }

    // _get_video_with_context_component: title from headline.runs[0].text; view text/short both shortViewCountText.
    private static JsonObject ParseVideoWithContext(JsonNode element)
    {
        JsonNode? video = Get(element, "videoWithContextRenderer");
        string? vid = String(video, "videoId");
        string? cid = String(video, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId");
        return Object(
            ("type", Value("video")), ("id", Value(vid)),
            ("title", Value(String(video, "headline", "runs", 0, "text"))),
            ("publishedTime", Value(String(video, "publishedTimeText", "runs", 0, "text"))),
            ("duration", Value(String(video, "lengthText", "runs", 0, "text"))),
            ("viewCount", Object(
                ("text", Value(String(video, "shortViewCountText", "runs", 0, "text"))),
                ("short", Value(String(video, "shortViewCountText", "runs", 0, "text"))))),
            ("thumbnails", Get(video, "thumbnail", "thumbnails")),
            ("channel", Object(
                ("name", Value(String(video, "shortBylineText", "runs", 0, "text"))),
                ("id", Value(cid)),
                ("link", Value(cid is null ? null : $"https://www.youtube.com/channel/{cid}")))),
            ("accessibility", Object(
                ("title", Value(String(video, "headline", "accessibility", "accessibilityData", "label"))),
                ("duration", Value(String(video, "lengthText", "accessibility", "accessibilityData", "label"))))),
            ("link", Value(vid is null ? null : $"https://www.youtube.com/watch?v={vid}")));
    }
    // _get_compact_playlist_component.
    private static JsonObject ParseCompactPlaylist(JsonNode element)
    {
        JsonNode? playlist = Get(element, "compactPlaylistRenderer");
        string? pid = String(playlist, "playlistId");
        string? cid = String(playlist, "shortBylineText", "runs", 0, "navigationEndpoint", "browseEndpoint", "browseId");
        return Object(
            ("type", Value("playlist")), ("id", Value(pid)),
            ("title", Value(String(playlist, "title", "simpleText"))),
            ("videoCount", Value(String(playlist, "videoCountShortText", "simpleText"))),
            ("thumbnails", Get(playlist, "thumbnail", "thumbnails")),
            ("channel", Object(
                ("name", Value(String(playlist, "shortBylineText", "runs", 0, "text"))),
                ("id", Value(cid)),
                ("link", Value(cid is null ? null : $"https://www.youtube.com/channel/{cid}")))),
            ("link", Value(pid is null ? null : $"https://www.youtube.com/playlist?list={pid}")));
    }
}

/// <summary>
/// Public recommendations surface, mirroring extras.py Recommendations: get_home drives a
/// BrowseCore over "FEwhat_to_watch"; get_related drives a RelatedVideosCore. Both default to
/// a 20s timeout with no retries, exactly as py_yt.
/// </summary>
public static class Recommendations
{
    public static Task<JsonObject> GetHomeAsync(int limit = 20, string language = "en", string region = "US",
        TimeSpan? timeout = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        new Browse("FEwhat_to_watch", limit, language, region, timeout ?? TimeSpan.FromSeconds(20), httpClient)
            .NextAsync(cancellationToken);

    public static Task<JsonObject> GetRelatedAsync(string videoLink, int limit = 20, string language = "en",
        string region = "US", TimeSpan? timeout = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        new RelatedVideos(videoLink, limit, language, region, timeout ?? TimeSpan.FromSeconds(20), httpClient)
            .NextAsync(cancellationToken);
}
