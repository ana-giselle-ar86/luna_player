using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static PyYt.JsonNavigator;

namespace PyYt;

public abstract class SearchBase : IDisposable
{
    private readonly YouTubeHttpClient _http;
    private readonly bool _findVideos;
    private readonly bool _findChannels;
    private readonly bool _findPlaylists;
    private string? _continuation;

    protected SearchBase(string query, int limit, string language, string region, string? searchPreferences,
        bool withLive, int maxRetries, HttpClient? httpClient, TimeSpan? timeout,
        bool findVideos, bool findChannels, bool findPlaylists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        Query = query;
        Limit = limit;
        Language = language;
        Region = region;
        SearchPreferences = searchPreferences;
        WithLive = withLive;
        _findVideos = findVideos;
        _findChannels = findChannels;
        _findPlaylists = findPlaylists;
        _http = new YouTubeHttpClient(httpClient, timeout ?? TimeSpan.FromSeconds(7), maxRetries);
    }

    public string Query { get; }
    public int Limit { get; }
    public string Language { get; }
    public string Region { get; }
    public string? SearchPreferences { get; }
    public bool WithLive { get; }
    public bool HasMoreResults => _continuation is not null;

    public async Task<JsonObject> NextAsync(CancellationToken cancellationToken = default)
    {
        JsonNode response = await _http.PostJsonAsync(YouTubeConstants.ApiUrl("search"), CreateBody(), cancellationToken).ConfigureAwait(false);
        JsonArray source = ParseSource(response);
        var results = new JsonArray();
        foreach (JsonNode? elementNode in source)
        {
            if (elementNode is not JsonObject element) continue;
            if (_findVideos && element.ContainsKey("videoRenderer")) AddVideo(results, element);
            if (_findChannels && element.ContainsKey("channelRenderer")) Add(results, ComponentParser.ParseChannel(element));
            if (_findPlaylists && (element.ContainsKey("playlistRenderer") || element.ContainsKey("lockupViewModel")))
                Add(results, ComponentParser.ParsePlaylist(element));
            if (_findVideos && Get(element, "shelfRenderer", "content", "verticalListRenderer", "items") is JsonArray shelfItems)
            {
                string? title = String(element, "shelfRenderer", "title", "simpleText");
                foreach (JsonNode? shelfItem in shelfItems)
                    if (shelfItem is JsonObject shelfObject && shelfObject.ContainsKey("videoRenderer")) AddVideo(results, shelfObject, title);
            }
            if (_findVideos && Get(element, "richItemRenderer", "content") is JsonObject rich && rich.ContainsKey("videoRenderer")) AddVideo(results, rich);
            if (results.Count >= Limit) break;
        }
        return new JsonObject { ["result"] = results };
    }

    private void AddVideo(JsonArray results, JsonObject element, string? shelfTitle = null)
    {
        JsonObject video = ComponentParser.ParseVideo(element, shelfTitle);
        if (!WithLive && video["duration"] is null && video["publishedTime"] is null) return;
        Add(results, video);
    }

    private JsonObject CreateBody()
    {
        // Mirrors SearchCore._get_request_body: when the query is itself a video id or a
        // recognized video URL, search for the bare id and drop any searchPreferences params.
        string query = Query;
        bool isVideoIdOrUrl = TryExtractVideoId(Query, out string? videoId);
        if (isVideoIdOrUrl && videoId is not null) query = videoId;
        string? searchParams = (!isVideoIdOrUrl && !string.IsNullOrEmpty(SearchPreferences)) ? SearchPreferences : null;
        return RequestPayload.Build(
            language: Language,
            region: Region,
            continuation: _continuation,
            @params: searchParams,
            extraFields: new (string, JsonNode?)[] { ("query", JsonValue.Create(query)) });
    }

    // The five video-id/url patterns from SearchCore._get_request_body, in order.
    private static bool TryExtractVideoId(string query, out string? videoId)
    {
        foreach (Regex pattern in VideoIdPatterns.All)
        {
            Match match = pattern.Match(query);
            if (match.Success) { videoId = match.Groups[1].Value; return true; }
        }
        videoId = null;
        return false;
    }

    private JsonArray ParseSource(JsonNode response)
    {
        bool wasContinuation = _continuation is not null;
        // SearchCore.next() never clears the continuation token, but Luna's paging contract
        // requires HasMoreResults to fall to false once a page carries no further token, so the
        // token is re-derived from scratch on every page (the sole deliberate divergence).
        _continuation = null;
        JsonNode? content = wasContinuation
            ? Get(response, "onResponseReceivedCommands", 0, "appendContinuationItemsAction", "continuationItems")
            : Get(response, "contents", "twoColumnSearchResultsRenderer", "primaryContents", "sectionListRenderer", "contents");

        var source = new JsonArray();
        if (content is JsonArray items)
        {
            // Primary layout: responseSource is the LAST itemSectionRenderer's contents (last wins),
            // and any continuationItemRenderer sibling supplies the next-page token.
            foreach (JsonNode? item in items)
            {
                if (Get(item, "itemSectionRenderer", "contents") is JsonArray section)
                {
                    source = new JsonArray();
                    foreach (JsonNode? child in section) source.Add(Copy(child));
                }
                string? token = String(item, "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
                if (token is not null) _continuation = token;
            }
            return source;
        }

        // Fallback grid layout: responseSource is the grid contents and the continuation token,
        // when present, rides on the final element.
        if (Get(response, "contents", "twoColumnSearchResultsRenderer", "primaryContents", "richGridRenderer", "contents") is JsonArray grid)
        {
            foreach (JsonNode? child in grid) source.Add(Copy(child));
            if (grid.Count > 0)
                _continuation = String(grid[^1], "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
        }
        return source;
    }

    public void Dispose() => _http.Dispose();
}

public sealed class Search : SearchBase
{
    public Search(string query, int limit = 20, string language = "en", string region = "US", TimeSpan? timeout = null,
        bool withLive = true, int maxRetries = 2, HttpClient? httpClient = null)
        : base(query, limit, language, region, null, withLive, maxRetries, httpClient, timeout, true, true, true) { }
}

public sealed class VideosSearch : SearchBase
{
    public VideosSearch(string query, int limit = 20, string language = "en", string region = "US", TimeSpan? timeout = null,
        bool withLive = true, int maxRetries = 2, HttpClient? httpClient = null)
        : base(query, limit, language, region, SearchMode.Videos, withLive, maxRetries, httpClient, timeout, true, false, false) { }
}

public sealed class ChannelsSearch : SearchBase
{
    public ChannelsSearch(string query, int limit = 20, string language = "en", string region = "US", TimeSpan? timeout = null,
        int maxRetries = 2, HttpClient? httpClient = null)
        : base(query, limit, language, region, SearchMode.Channels, true, maxRetries, httpClient, timeout, false, true, false) { }
}

public sealed class PlaylistsSearch : SearchBase
{
    public PlaylistsSearch(string query, int limit = 20, string language = "en", string region = "US", TimeSpan? timeout = null,
        int maxRetries = 2, HttpClient? httpClient = null)
        : base(query, limit, language, region, SearchMode.Playlists, true, maxRetries, httpClient, timeout, false, false, true) { }
}

public sealed class CustomSearch : SearchBase
{
    public CustomSearch(string query, string searchPreferences, int limit = 20, string language = "en", string region = "US",
        TimeSpan? timeout = null, bool withLive = true, int maxRetries = 2, HttpClient? httpClient = null)
        : base(query, limit, language, region, searchPreferences, withLive, maxRetries, httpClient, timeout, true, true, true) { }
}

/// <summary>
/// The five video-id / video-URL patterns applied by SearchCore._get_request_body, in order.
/// Source-generated so they stay trim- and NativeAOT-safe.
/// </summary>
internal static partial class VideoIdPatterns
{
    internal static readonly Regex[] All = [Watch(), ShortLink(), Embed(), LegacyV(), BareId()];

    [GeneratedRegex(@"youtube\.com/watch\?v=([a-zA-Z0-9_-]{11})")]
    private static partial Regex Watch();

    [GeneratedRegex(@"youtu\.be/([a-zA-Z0-9_-]{11})")]
    private static partial Regex ShortLink();

    [GeneratedRegex(@"youtube\.com/embed/([a-zA-Z0-9_-]{11})")]
    private static partial Regex Embed();

    [GeneratedRegex(@"youtube\.com/v/([a-zA-Z0-9_-]{11})")]
    private static partial Regex LegacyV();

    [GeneratedRegex(@"^([a-zA-Z0-9_-]{11})$")]
    private static partial Regex BareId();
}
