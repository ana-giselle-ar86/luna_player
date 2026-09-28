using System.Text.Json.Nodes;

namespace PyYt;

/// <summary>
/// Shared InnerTube constants and request-building helpers, mirroring
/// py_yt's core/constants.py and the URL/payload helpers on core/requests.py.
/// </summary>
public static class YouTubeConstants
{
    /// <summary>InnerTube API key (py_yt: searchKey).</summary>
    public const string SearchKey = "AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";

    /// <summary>Default WEB client version (py_yt: requestPayload clientVersion).</summary>
    public const string ClientVersion = "2.20260820.08.00";

    /// <summary>Default desktop Chrome User-Agent (py_yt: userAgent).</summary>
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";

    /// <summary>
    /// Builds a YouTube InnerTube endpoint URL, mirroring RequestCore._build_url.
    /// When <paramref name="prettyPrint"/> is false a <c>prettyPrint=false</c> parameter is added
    /// (py_yt passes <c>{"prettyPrint": "false"}</c> as an extra parameter for browse/next/get_transcript).
    /// </summary>
    public static string ApiUrl(string endpoint, bool prettyPrint = true)
    {
        string url = $"https://www.youtube.com/youtubei/v1/{endpoint}?key={SearchKey}";
        return prettyPrint ? url : url + "&prettyPrint=false";
    }

    /// <summary>Rotating client profiles used for retry cycling (py_yt: CLIENT_PROFILES).</summary>
    public static readonly IReadOnlyDictionary<string, ClientProfile> ClientProfiles =
        new Dictionary<string, ClientProfile>(StringComparer.Ordinal)
    {
        ["WEB"] = new ClientProfile("WEB", "2.20260820.08.00", UserAgent, "1"),
        ["ANDROID_VR"] = new ClientProfile("ANDROID_VR", "1.61.26",
            "Mozilla/5.0 (Linux; Android 12; Quest 3) AppleWebKit/537.36 (KHTML, like Gecko) OculusBrowser/32.0.0.3.17 Chrome/122.0.6261.64 Mobile Safari/537.36",
            "93"),
        ["MWEB"] = new ClientProfile("MWEB", "2.20260821.00.00",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1",
            "2"),
        ["TVHTML5"] = new ClientProfile("TVHTML5", "7.20260819.16.00",
            "Mozilla/5.0 (ChromiumStylePlatform) Cobalt/Version", "7"),
        ["ANDROID_TESTSUITE"] = new ClientProfile("ANDROID_TESTSUITE", "1.9",
            "com.google.android.apps.youtube.unplugged/1.9 (Linux; U; Android 12)", "82"),
    };

    /// <summary>Order in which profiles are cycled across retries (py_yt: CLIENT_PROFILE_KEYS).</summary>
    public static readonly string[] ClientProfileKeys = ["WEB", "MWEB", "ANDROID_VR", "TVHTML5"];

    /// <summary>Maps a clientName to its X-YouTube-Client-Name code (py_yt: client_name_map).</summary>
    public static readonly IReadOnlyDictionary<string, string> ClientNameMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["WEB"] = "1",
        ["MWEB"] = "2",
        ["ANDROID"] = "3",
        ["IOS"] = "5",
        ["TVHTML5"] = "7",
        ["ANDROID_TESTSUITE"] = "82",
        ["ANDROID_VR"] = "93",
    };
}

/// <summary>A single InnerTube client profile (py_yt: an entry of CLIENT_PROFILES).</summary>
public sealed class ClientProfile(string clientName, string clientVersion, string userAgent, string clientCode)
{
    public string ClientName { get; } = clientName;
    public string ClientVersion { get; } = clientVersion;
    public string UserAgent { get; } = userAgent;
    public string ClientCode { get; } = clientCode;
}

/// <summary>Builds the standard InnerTube request payload (py_yt: requestPayload template).</summary>
internal static class RequestPayload
{
    internal static JsonObject Create(string clientVersion = YouTubeConstants.ClientVersion) => new()
    {
        ["context"] = new JsonObject
        {
            ["client"] = new JsonObject
            {
                ["hl"] = "en",
                ["gl"] = "US",
                ["clientName"] = "WEB",
                ["clientVersion"] = clientVersion,
                ["newVisitorCookie"] = true,
            },
            ["user"] = new JsonObject { ["lockedSafetyMode"] = false },
        },
    };

    /// <summary>
    /// Mirrors RequestCore._build_payload: deep-copies the requestPayload template, sets
    /// context.client hl/gl/clientName/clientVersion, applies params/continuation when truthy,
    /// and writes each non-null extra field at the payload top level.
    /// </summary>
    internal static JsonObject Build(
        string language = "en",
        string region = "US",
        string clientName = "WEB",
        string clientVersion = YouTubeConstants.ClientVersion,
        string? continuation = null,
        string? @params = null,
        params (string Key, JsonNode? Value)[] extraFields)
    {
        JsonObject payload = Create(clientVersion);
        var client = (JsonObject)payload["context"]!["client"]!;
        client["hl"] = language;
        client["gl"] = region;
        client["clientName"] = clientName;
        client["clientVersion"] = clientVersion;
        if (!string.IsNullOrEmpty(@params)) payload["params"] = @params;
        if (!string.IsNullOrEmpty(continuation)) payload["continuation"] = continuation;
        foreach ((string key, JsonNode? value) in extraFields)
            if (value is not null) payload[key] = value;
        return payload;
    }
}

/// <summary>Result output format, mirroring py_yt's ResultMode.</summary>
public static class ResultMode
{
    public const int Json = 0;
    public const int Dict = 1;
}

public static class SearchMode
{
    public const string Videos = "EgIQAQ%3D%3D";
    public const string Channels = "EgIQAg%3D%3D";
    public const string Playlists = "EgIQAw%3D%3D";
    public const string Livestreams = "EgJAAQ%3D%3D";
}

public static class VideoUploadDateFilter
{
    public const string LastHour = "EgQIARAB";
    public const string Today = "EgQIAhAB";
    public const string ThisWeek = "EgQIAxAB";
    public const string ThisMonth = "EgQIBBAB";
    public const string ThisYear = "EgQIBRAB";
}

public static class VideoDurationFilter
{
    public const string Short = "EgQQARgB";
    public const string Long = "EgQQARgC";
}

public static class VideoSortOrder
{
    public const string Relevance = "CAASAhAB";
    public const string UploadDate = "CAISAhAB";
    public const string ViewCount = "CAMSAhAB";
    public const string Rating = "CAESAhAB";
}

public static class ChannelRequestType
{
    public const string Info = "EgVhYm91dA%3D%3D";
    public const string Playlists = "EglwbGF5bGlzdHPyBgQKAkIA";
}
