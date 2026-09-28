using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

public sealed class Hashtag : IDisposable
{
    private readonly YouTubeHttpClient _http;
    private readonly string _hashtag;
    private readonly int _limit;
    private readonly string _language;
    private readonly string _region;
    private string? _parameters;
    private string? _continuation;

    public Hashtag(string hashtag, int limit = 60, string language = "en", string region = "US", TimeSpan? timeout = null,
        HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashtag);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        _hashtag = hashtag.TrimStart('#');
        _limit = limit;
        _language = language;
        _region = region;
        _http = new YouTubeHttpClient(httpClient, timeout ?? TimeSpan.FromSeconds(7), 2);
    }

    public bool HasMoreResults => _continuation is not null;

    public async Task<JsonObject> NextAsync(CancellationToken cancellationToken = default)
    {
        if (_parameters is null) await LoadParametersAsync(cancellationToken).ConfigureAwait(false);
        // HashtagCore._make_request does nothing while params is still unknown, so there is no page.
        if (_parameters is null) return new JsonObject { ["result"] = new JsonArray() };

        bool continuationRequest = _continuation is not null;
        JsonObject body = RequestPayload.Build(
            language: _language,
            region: _region,
            continuation: _continuation,
            @params: _parameters,
            extraFields: new (string, JsonNode?)[] { ("browseId", JsonValue.Create("FEhashtag")) });
        JsonNode response = await _http.PostJsonAsync(YouTubeConstants.ApiUrl("browse"), body, cancellationToken).ConfigureAwait(false);
        JsonNode? source = continuationRequest
            ? Get(response, "onResponseReceivedActions", 0, "appendContinuationItemsAction", "continuationItems")
            : Get(response, "contents", "twoColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content", "richGridRenderer", "contents");
        var results = new JsonArray();
        _continuation = null;
        if (source is JsonArray items && items.Count > 0)
        {
            foreach (JsonNode? item in items)
            {
                if (Get(item, "richItemRenderer", "content") is JsonObject content && content.ContainsKey("videoRenderer"))
                    Add(results, ComponentParser.ParseVideo(content));
                if (results.Count >= _limit) break;
            }
            // The continuation token always rides on the final element of the source list.
            _continuation = String(items[^1], "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token");
        }
        return new JsonObject { ["result"] = results };
    }

    private async Task LoadParametersAsync(CancellationToken cancellationToken)
    {
        JsonObject body = RequestPayload.Build(
            language: _language,
            region: _region,
            extraFields: new (string, JsonNode?)[] { ("query", JsonValue.Create("#" + _hashtag)) });
        JsonNode response = await _http.PostJsonAsync(YouTubeConstants.ApiUrl("search"), body, cancellationToken).ConfigureAwait(false);
        if (Get(response, "contents", "twoColumnSearchResultsRenderer", "primaryContents", "sectionListRenderer", "contents",
                0, "itemSectionRenderer", "contents") is not JsonArray items) return;
        foreach (JsonNode? item in items)
        {
            _parameters = String(item, "hashtagTileRenderer", "onTapCommand", "browseEndpoint", "params");
            if (_parameters is not null) return;
        }
    }

    public void Dispose() => _http.Dispose();
}
