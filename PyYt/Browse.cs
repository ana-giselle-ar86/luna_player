using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

/// <summary>
/// Mirrors py_yt's core/browse.py BrowseCore: the InnerTube "browse" endpoint driver
/// behind Recommendations.get_home. Uses the MWEB client (v2.20260821.00.00), no retries
/// and a 20s timeout, exactly as py_yt. Each NextAsync returns one page's components and
/// records the continuation token for the following call.
/// </summary>
internal sealed class Browse
{
    private readonly string _browseId;
    private readonly int _limit;
    private readonly string _language;
    private readonly string _region;
    private readonly TimeSpan _timeout;
    private readonly HttpClient? _httpClient;
    private string? _continuation;

    internal Browse(string browseId, int limit, string language, string region, TimeSpan timeout, HttpClient? httpClient)
    {
        _browseId = browseId;
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
            _continuation, null, ("browseId", JsonValue.Create(_browseId)));
        JsonNode response = await http.PostJsonAsync(YouTubeConstants.ApiUrl("browse"), body, cancellationToken)
            .ConfigureAwait(false);
        ParseSource(response, components);
        return new JsonObject { ["result"] = components };
    }
    // Mirrors BrowseCore._parse_source.
    private void ParseSource(JsonNode? response, JsonArray components)
    {
        JsonNode? items = null;
        if (response is JsonObject robj && robj.ContainsKey("contents"))
        {
            JsonNode? tabContent =
                Get(response, "contents", "twoColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content")
                ?? Get(response, "contents", "singleColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content")
                ?? Get(response, "contents");
            items = tabContent is JsonArray list
                ? list
                : Get(tabContent, "richGridRenderer", "contents") ?? Get(tabContent, "sectionListRenderer", "contents");
        }
        else if (response is JsonObject actions && actions.ContainsKey("onResponseReceivedActions"))
        {
            items = Get(response, "onResponseReceivedActions", 0, "appendContinuationItemsAction", "continuationItems");
        }

        if (items is not JsonArray array) return;
        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject) continue;

            if (Get(item, "richItemRenderer") is not null)
            {
                JsonNode? content = Get(item, "richItemRenderer", "content");
                if (Get(content, "videoRenderer") is not null) Add(components, ComponentParser.ParseVideo(content!));
                else if (Get(content, "playlistRenderer") is not null) Add(components, ComponentParser.ParsePlaylist(content!));
            }
            else if (Get(item, "videoRenderer") is not null) Add(components, ComponentParser.ParseVideo(item));
            else if (Get(item, "playlistRenderer") is not null) Add(components, ComponentParser.ParsePlaylist(item));
            else if (Get(item, "richSectionRenderer") is not null)
            {
                // richShelfRenderer nested videos are appended without a per-shelf limit check,
                // exactly as py_yt (the limit is only enforced once per top-level element).
                if (Get(item, "richSectionRenderer", "content", "richShelfRenderer", "contents") is JsonArray shelf)
                    foreach (JsonNode? shelfItem in shelf)
                        if (Get(shelfItem, "richItemRenderer", "content") is JsonNode shelfContent
                            && Get(shelfContent, "videoRenderer") is not null)
                            Add(components, ComponentParser.ParseVideo(shelfContent));
            }
            else if (Get(item, "continuationItemRenderer") is not null)
            {
                string? token = String(item, "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
                _continuation = string.IsNullOrEmpty(token) ? null : token;
            }

            if (components.Count >= _limit) break;
        }
    }
}
