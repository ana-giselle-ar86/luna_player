using System.Text.Json.Nodes;

namespace PyYt;

public static class Suggestions
{
    public static async Task<JsonObject> GetAsync(string query, string language = "en", string region = "US",
        TimeSpan? timeout = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        string url = "https://clients1.google.com/complete/search" +
            $"?hl={Uri.EscapeDataString(language)}&gl={Uri.EscapeDataString(region)}&q={Uri.EscapeDataString(query)}&client=youtube&gs_ri=youtube&ds=yt";
        using var http = new YouTubeHttpClient(httpClient, timeout ?? TimeSpan.FromSeconds(7), 2);
        string response = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        int start = response.IndexOf("([", StringComparison.Ordinal);
        int end = response.LastIndexOf("])", StringComparison.Ordinal);
        if (start < 0 || end <= start) throw new PyYtException("Could not parse the YouTube suggestions response.");
        JsonNode root = JsonNode.Parse(response.Substring(start + 1, end - start))
            ?? throw new PyYtException("YouTube returned empty suggestions.");
        var suggestions = new JsonArray();
        if (root is JsonArray outer)
        {
            foreach (JsonNode? item in outer)
            {
                if (item is not JsonArray candidates) continue;
                foreach (JsonNode? candidate in candidates)
                    if (candidate is JsonArray values && values.Count > 0)
                        suggestions.Add(JsonNavigator.Copy(values[0]));
                break;
            }
        }
        return new JsonObject { ["result"] = suggestions };
    }
}
