using System.Text.Json.Nodes;
using static PyYt.JsonNavigator;

namespace PyYt;

/// <summary>
/// Mirrors py_yt's core/video.py VideoCore together with the extras.Video wrappers
/// (get / get_info / get_formats). The InnerTube "player" endpoint is queried with the
/// video id, contentCheckOk and racyCheckOk carried as URL query parameters, and the
/// per-client payload body from CLIENTS (whose context.client is re-stamped per retry
/// profile by the transport, exactly as py_yt does).
/// </summary>
public static class Video
{
    // extras.Video.get -> VideoCore(component_mode="", enable_html=get_upload_date).
    public static Task<JsonObject> GetAsync(string videoLink, TimeSpan? timeout = null, bool getUploadDate = false,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        RunAsync(videoLink, componentMode: "", enableHtml: getUploadDate, callCreate: true,
            timeout ?? TimeSpan.FromSeconds(2), httpClient, cancellationToken);

    // extras.Video.get_info -> VideoCore(component_mode="getInfo", enable_html=True); only html_create + processing.
    public static Task<JsonObject> GetInfoAsync(string videoLink, TimeSpan? timeout = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        RunAsync(videoLink, componentMode: "getInfo", enableHtml: true, callCreate: false,
            timeout ?? TimeSpan.FromSeconds(2), httpClient, cancellationToken);

    // extras.Video.get_formats -> VideoCore(component_mode="getFormats", enable_html=False); create only.
    public static Task<JsonObject> GetFormatsAsync(string videoLink, TimeSpan? timeout = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        RunAsync(videoLink, componentMode: "getFormats", enableHtml: false, callCreate: true,
            timeout ?? TimeSpan.FromSeconds(2), httpClient, cancellationToken);

    private static async Task<JsonObject> RunAsync(string videoLink, string? componentMode, bool enableHtml,
        bool callCreate, TimeSpan timeout, HttpClient? httpClient, CancellationToken cancellationToken)
    {
        string cleanedLink = CleanUrl(videoLink);
        string videoId = GetVideoId(cleanedLink);
        using var http = new YouTubeHttpClient(httpClient, timeout, 2);

        // html_create(): the MWEB client body populates HTMLresponseSource.
        JsonNode? htmlResponseSource = null;
        if (enableHtml)
            htmlResponseSource = await http.PostJsonAsync(PlayerUrl(videoId), ClientBody("MWEB"), cancellationToken).ConfigureAwait(false);

        // create(): the overridden (ANDROID) client body populates responseSource.
        JsonNode? responseSource = null;
        if (callCreate)
            responseSource = await http.PostJsonAsync(PlayerUrl(videoId), ClientBody("ANDROID"), cancellationToken).ConfigureAwait(false);

        return BuildComponent(componentMode, responseSource, htmlResponseSource, enableHtml);
    }

    // Mirrors VideoCore._get_video_component.
    private static JsonObject BuildComponent(string? mode, JsonNode? responseSource, JsonNode? htmlResponseSource, bool enableHtml)
    {
        var component = new JsonObject();
        if (mode is null || mode == "getInfo")
        {
            JsonNode? src = responseSource;
            if (enableHtml && htmlResponseSource is not null) src = htmlResponseSource;
            string? vid = String(src, "videoDetails", "videoId");
            string? cid = String(src, "videoDetails", "channelId");
            string? secondsText = String(src, "videoDetails", "lengthSeconds");
            bool? isLiveContent = Boolean(src, "videoDetails", "isLiveContent");
            component["id"] = vid;
            component["title"] = String(src, "videoDetails", "title");
            component["duration"] = new JsonObject { ["secondsText"] = secondsText };
            component["viewCount"] = new JsonObject { ["text"] = String(src, "videoDetails", "viewCount") };
            component["thumbnails"] = Copy(Get(src, "videoDetails", "thumbnail", "thumbnails"));
            component["description"] = String(src, "videoDetails", "shortDescription");
            component["channel"] = new JsonObject
            {
                ["name"] = String(src, "videoDetails", "author"),
                ["id"] = cid,
                ["link"] = cid is null ? null : $"https://www.youtube.com/channel/{cid}",
            };
            component["allowRatings"] = Copy(Get(src, "videoDetails", "allowRatings"));
            component["averageRating"] = Copy(Get(src, "videoDetails", "averageRating"));
            component["keywords"] = Copy(Get(src, "videoDetails", "keywords"));
            component["isLiveContent"] = Copy(Get(src, "videoDetails", "isLiveContent"));
            component["publishDate"] = Copy(Get(src, "microformat", "playerMicroformatRenderer", "publishDate"));
            component["uploadDate"] = Copy(Get(src, "microformat", "playerMicroformatRenderer", "uploadDate"));
            component["isFamilySafe"] = Copy(Get(src, "microformat", "playerMicroformatRenderer", "isFamilySafe"));
            component["category"] = Copy(Get(src, "microformat", "playerMicroformatRenderer", "category"));
            component["link"] = vid is null ? null : $"https://www.youtube.com/watch?v={vid}";
            component["isLiveNow"] = (isLiveContent ?? false) && secondsText == "0";
        }
        if (mode is null || mode == "getFormats")
            component["streamingData"] = Copy(Get(responseSource, "streamingData"));
        if (enableHtml && htmlResponseSource is not null)
        {
            component["publishDate"] = Copy(Get(htmlResponseSource, "microformat", "playerMicroformatRenderer", "publishDate"));
            component["uploadDate"] = Copy(Get(htmlResponseSource, "microformat", "playerMicroformatRenderer", "uploadDate"));
        }
        return component;
    }

    // Mirrors _get_cleaned_url: collapse a watch URL down to its bare v= parameter.
    private static string CleanUrl(string videoLink)
    {
        if (Uri.TryCreate(videoLink, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Query))
        {
            foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "v")
                    return $"https://www.youtube.com/watch?v={Uri.UnescapeDataString(kv[1])}";
            }
        }
        return videoLink;
    }

    private static string PlayerUrl(string videoId) =>
        $"https://www.youtube.com/youtubei/v1/player?key={YouTubeConstants.SearchKey}" +
        $"&contentCheckOk=true&racyCheckOk=true&videoId={Uri.EscapeDataString(videoId)}";

    // Mirrors video.py CLIENTS: every entry carries context.client + api_key; the transport
    // re-stamps clientName/clientVersion from the active retry profile on each request.
    private static JsonObject ClientBody(string clientKey)
    {
        JsonObject client = clientKey switch
        {
            "ANDROID_EMBED" => new JsonObject
                { ["clientName"] = "WEB", ["clientVersion"] = "2.20260820.08.00", ["clientScreen"] = "EMBED" },
            "TV_EMBED" => new JsonObject
                { ["clientName"] = "TVHTML5_SIMPLY_EMBEDDED_PLAYER", ["clientVersion"] = "2.0" },
            _ => new JsonObject { ["clientName"] = "WEB", ["clientVersion"] = "2.20260820.08.00" },
        };
        var context = new JsonObject { ["client"] = client };
        if (clientKey == "TV_EMBED")
            context["thirdParty"] = new JsonObject { ["embedUrl"] = "https://www.youtube.com/" };
        return new JsonObject { ["context"] = context, ["api_key"] = YouTubeConstants.SearchKey };
    }

    public static string GetVideoId(string videoLink) => JsonNavigator.GetVideoId(videoLink);
}
