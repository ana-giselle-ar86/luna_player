using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

public static class Transcript
{
    public static async Task<JsonObject> GetAsync(string videoLink, string? parameters = null, TimeSpan? timeout = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        using var http = new YouTubeHttpClient(httpClient, timeout ?? TimeSpan.FromSeconds(7), 2);
        if (string.IsNullOrEmpty(parameters))
        {
            JsonObject body = RequestPayload.Build(
                extraFields: new (string, JsonNode?)[] { ("videoId", JsonValue.Create(GetVideoId(videoLink))) });
            JsonNode initial = await http.PostJsonAsync(YouTubeConstants.ApiUrl("next", false), body, cancellationToken).ConfigureAwait(false);
            parameters = FindParameters(initial);
            if (string.IsNullOrEmpty(parameters)) return EmptyResult();
        }
        JsonObject transcriptBody = RequestPayload.Build(@params: parameters);
        JsonNode response = await http.PostJsonAsync(YouTubeConstants.ApiUrl("get_transcript", false), transcriptBody, cancellationToken).ConfigureAwait(false);
        return Parse(response);
    }

    private static string? FindParameters(JsonNode response)
    {
        // Mirrors TranscriptCore.extract_continuation_key: absent panels yield an empty result
        // (not an error), and the LAST matching panel's params win.
        if (Get(response, "engagementPanels") is not JsonArray panels) return null;
        string? key = null;
        foreach (JsonNode? panel in panels)
        {
            JsonNode? renderer = Get(panel, "engagementPanelSectionListRenderer");
            if (String(renderer, "targetId") == "engagement-panel-searchable-transcript")
                key = String(renderer, "content", "continuationItemRenderer", "continuationEndpoint", "getTranscriptEndpoint", "params");
        }
        return key;
    }

    private static JsonObject Parse(JsonNode response)
    {
        var segments = new JsonArray();
        if (Get(response, "actions", 0, "updateEngagementPanelAction", "content", "transcriptRenderer", "content",
            "transcriptSearchPanelRenderer", "body", "transcriptSegmentListRenderer", "initialSegments") is JsonArray transcriptSegments)
        {
            foreach (JsonNode? item in transcriptSegments)
            {
                JsonNode? segment = Get(item, "transcriptSegmentRenderer");
                if (segment is null) continue;
                Add(segments, Object(
                    ("startMs", Get(segment, "startMs")), ("endMs", Get(segment, "endMs")),
                    ("text", Value(String(segment, "snippet", "runs", 0, "text"))),
                    ("startTime", Value(String(segment, "startTimeText", "simpleText")))));
            }
        }
        var languages = new JsonArray();
        if (Get(response, "actions", 0, "updateEngagementPanelAction", "content", "transcriptRenderer", "content",
            "transcriptSearchPanelRenderer", "footer", "transcriptFooterRenderer", "languageMenu", "sortFilterSubMenuRenderer", "subMenuItems") is JsonArray languageItems)
        {
            foreach (JsonNode? language in languageItems)
                Add(languages, Object(
                    ("params", Value(String(language, "continuation", "reloadContinuationData", "continuation"))),
                    ("selected", Get(language, "selected")), ("title", Value(String(language, "title")))));
        }
        return new JsonObject { ["segments"] = segments, ["languages"] = languages };
    }

    private static JsonObject EmptyResult() => new() { ["segments"] = new JsonArray(), ["languages"] = new JsonArray() };
}
