using System.Text.Json.Nodes;

namespace PyYt;

internal static class JsonNavigator
{
    internal static JsonNode? Get(JsonNode? source, params object[] path)
    {
        JsonNode? current = source;
        foreach (object part in path)
        {
            if (current is null) return null;
            if (part is string key)
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(key, out current)) return null;
            }
            else if (part is int requestedIndex)
            {
                // Mirrors py_yt get_value: an int key resolves only when 0 <= key < len;
                // negative indexes are NOT wrapped and yield null.
                if (current is not JsonArray array) return null;
                if (requestedIndex < 0 || requestedIndex >= array.Count) return null;
                current = array[requestedIndex];
            }
            else
            {
                throw new ArgumentException("JSON paths may contain only string keys and integer indexes.", nameof(path));
            }
        }
        return current;
    }

    internal static string? String(JsonNode? source, params object[] path)
    {
        JsonNode? node = Get(source, path);
        if (node is null) return null;
        if (node is JsonValue value && value.TryGetValue<string>(out string? text)) return text;
        return node.ToString();
    }

    internal static bool? Boolean(JsonNode? source, params object[] path)
    {
        JsonNode? node = Get(source, path);
        return node is JsonValue value && value.TryGetValue<bool>(out bool result) ? result : null;
    }

    internal static JsonNode? Copy(JsonNode? node) => node?.DeepClone();

    internal static JsonObject Object(params (string Name, JsonNode? Value)[] properties)
    {
        var result = new JsonObject();
        foreach ((string name, JsonNode? value) in properties) result[name] = Copy(value);
        return result;
    }

    internal static JsonNode? Value(string? value) => value is null ? null : JsonValue.Create(value);
    internal static JsonNode? Value(bool? value) => value is null ? null : JsonValue.Create(value.Value);

    internal static void Add(JsonArray array, JsonNode? value) => ((IList<JsonNode?>)array).Add(value);

    /// <summary>
    /// Mirrors ComponentHandler.get_video_id exactly (string slicing, not Uri parsing):
    /// youtu.be → last path segment (or the one before a trailing slash); youtube.com →
    /// the v= value up to any &amp; (else the last path segment); anything else is returned as-is.
    /// </summary>
    public static string GetVideoId(string videoLink)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoLink);
        if (videoLink.Contains("youtu.be", StringComparison.Ordinal))
        {
            string[] parts = videoLink.Split('/');
            return videoLink.EndsWith('/') ? parts[^2] : parts[^1];
        }
        if (videoLink.Contains("youtube.com", StringComparison.Ordinal))
        {
            if (!videoLink.Contains('&'))
            {
                int vIndex = videoLink.IndexOf("v=", StringComparison.Ordinal);
                if (vIndex >= 0) return videoLink[(vIndex + 2)..];
                string[] parts = videoLink.Split('/');
                return parts[^1];
            }
            int v = videoLink.IndexOf("v=", StringComparison.Ordinal);
            int amp = videoLink.IndexOf('&', StringComparison.Ordinal);
            return videoLink[(v + 2)..amp];
        }
        return videoLink;
    }
}
