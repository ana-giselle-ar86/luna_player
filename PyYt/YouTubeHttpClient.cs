using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace PyYt;

/// <summary>
/// InnerTube transport layer mirroring py_yt's core/requests.py RequestCore: profile cycling
/// across retries, per-profile headers and payload injection, visitor-data resolution/reuse,
/// proof-of-origin token handling (botGuard intentionally unavailable), and 2^i backoff.
/// </summary>
internal sealed class YouTubeHttpClient : IDisposable
{
    private const string VisitorIdEndpoint = "visitor_id";

    private readonly HttpClient _client;
    private readonly bool _sessionMode;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;
    private readonly string? _visitorData;
    private readonly string? _poToken;

    internal YouTubeHttpClient(HttpClient? client, TimeSpan timeout, int maxRetries,
        string? visitorData = null, string? poToken = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetries);
        _maxRetries = maxRetries;
        _timeout = timeout;
        _visitorData = visitorData;
        _poToken = poToken;
        if (client is null)
        {
            _client = Session.SharedClient;
            _sessionMode = true;
        }
        else
        {
            _client = client;
            _sessionMode = false;
        }
    }

    private string? EffectiveVisitorData => _visitorData ?? Session.VisitorData;
    private string? EffectivePoToken => _poToken ?? Session.PoToken;

    internal async Task<JsonNode> PostJsonAsync(string url, JsonObject body, CancellationToken cancellationToken)
    {
        await ResolveTokensAsync(cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; ; attempt++)
        {
            ClientProfile profile = ProfileForAttempt(attempt);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                PrepareRequestForProfile(request, profile, body);
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
                using CancellationTokenSource linked = LinkedTimeout(cancellationToken, _timeout);
                using HttpResponseMessage response = await _client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using Stream stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
                JsonNode node = await JsonNode.ParseAsync(stream, cancellationToken: linked.Token).ConfigureAwait(false)
                    ?? throw new PyYtException("YouTube returned an empty JSON response.");
                ExtractVisitorDataFromResponse(response, node);
                return node;
            }
            catch (Exception exception) when (attempt < _maxRetries && IsTransient(exception, cancellationToken))
            {
                await BackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        await ResolveTokensAsync(cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; ; attempt++)
        {
            ClientProfile profile = ProfileForAttempt(attempt);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                PrepareRequestForProfile(request, profile, null);
                request.Headers.TryAddWithoutValidation("Cookie", "CONSENT=YES+1");
                using CancellationTokenSource linked = LinkedTimeout(cancellationToken, _timeout);
                using HttpResponseMessage response = await _client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string text = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
                ExtractVisitorDataFromResponse(response, null);
                return text;
            }
            catch (Exception exception) when (attempt < _maxRetries && IsTransient(exception, cancellationToken))
            {
                await BackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static ClientProfile ProfileForAttempt(int attempt)
    {
        string[] keys = YouTubeConstants.ClientProfileKeys;
        string name = keys[attempt % keys.Length];
        return YouTubeConstants.ClientProfiles[name];
    }

    private void PrepareRequestForProfile(HttpRequestMessage request, ClientProfile profile, JsonObject? body)
    {
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd(profile.UserAgent);
        request.Headers.TryAddWithoutValidation("Origin", "https://www.youtube.com");
        request.Headers.TryAddWithoutValidation("Referer", "https://www.youtube.com/");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

        string clientName = profile.ClientName;
        string clientVersion = profile.ClientVersion;
        string? visitor = EffectiveVisitorData;
        string? poToken = EffectivePoToken;

        if (body is not null && body["context"] is JsonObject context && context["client"] is JsonObject client)
        {
            client["clientName"] = profile.ClientName;
            client["clientVersion"] = profile.ClientVersion;
            clientName = client["clientName"]?.GetValue<string>() ?? profile.ClientName;
            clientVersion = client["clientVersion"]?.GetValue<string>() ?? profile.ClientVersion;

            if (!string.IsNullOrEmpty(visitor)) client["visitorData"] = visitor;
            else client.Remove("visitorData");

            if (!string.IsNullOrEmpty(poToken))
                client["serviceIntegrityDimensions"] = new JsonObject { ["poToken"] = poToken };
            else client.Remove("serviceIntegrityDimensions");
        }

        string clientCode = YouTubeConstants.ClientNameMap.TryGetValue(clientName, out string? code) ? code : profile.ClientCode;
        request.Headers.TryAddWithoutValidation("X-YouTube-Client-Name", clientCode);
        request.Headers.TryAddWithoutValidation("X-YouTube-Client-Version", clientVersion);
        if (!string.IsNullOrEmpty(visitor)) request.Headers.TryAddWithoutValidation("X-Goog-Visitor-Id", visitor);
    }

    private async Task ResolveTokensAsync(CancellationToken cancellationToken)
    {
        if (!_sessionMode) return;
        if (Session.VisitorData is not null && Session.PoToken is not null && Session.PoTokenVerifier is null) return;

        await Session.TokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Session.PoTokenVerifier is not null)
            {
                try
                {
                    (string? visitorData, string? poToken) = await Session.PoTokenVerifier(cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(visitorData)) Session.VisitorData = visitorData;
                    if (!string.IsNullOrEmpty(poToken)) Session.PoToken = poToken;
                }
                catch
                {
                    // A failing verifier must not abort the request; py_yt swallows it too.
                }
            }

            // botGuard proof-of-origin generation is intentionally unavailable (no JS engine);
            // py_yt's import fallback branch is treated as permanently taken.

            if (string.IsNullOrEmpty(Session.VisitorData))
            {
                string? fetched = await FetchAutomaticVisitorDataAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(fetched)) Session.VisitorData = fetched;
            }
        }
        finally
        {
            Session.TokenLock.Release();
        }
    }

    private async Task<string?> FetchAutomaticVisitorDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var body = new JsonObject
            {
                ["context"] = new JsonObject
                {
                    ["client"] = new JsonObject
                    {
                        ["clientName"] = "WEB",
                        ["clientVersion"] = YouTubeConstants.ClientVersion,
                    },
                },
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, YouTubeConstants.ApiUrl(VisitorIdEndpoint));
            request.Headers.UserAgent.ParseAdd(YouTubeConstants.UserAgent);
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using CancellationTokenSource linked = LinkedTimeout(cancellationToken, TimeSpan.FromSeconds(3));
            using HttpResponseMessage response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            JsonNode? node = await JsonNode.ParseAsync(stream, cancellationToken: linked.Token).ConfigureAwait(false);
            return JsonNavigator.String(node, "responseContext", "visitorData");
        }
        catch
        {
            return null;
        }
    }

    private void ExtractVisitorDataFromResponse(HttpResponseMessage response, JsonNode? node)
    {
        if (!_sessionMode) return;
        if (response.Headers.TryGetValues("X-Goog-Visitor-Id", out IEnumerable<string>? headerValues))
        {
            string? headerVisitor = headerValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(headerVisitor)) { Session.VisitorData = headerVisitor; return; }
        }
        if (node is null) return;
        string? visitor = JsonNavigator.String(node, "responseContext", "visitorData")
            ?? JsonNavigator.String(node, "responseHeader", "visitorData")
            ?? JsonNavigator.String(node, "visitorData");
        if (!string.IsNullOrEmpty(visitor)) Session.VisitorData = visitor;
    }

    private static async Task BackoffAsync(int attempt, CancellationToken cancellationToken) =>
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return false;
        return exception is HttpRequestException or TaskCanceledException or OperationCanceledException;
    }

    private static CancellationTokenSource LinkedTimeout(CancellationToken cancellationToken, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan) cts.CancelAfter(timeout);
        return cts;
    }

    public void Dispose()
    {
        // The shared client is owned by Session; injected clients are owned by the caller.
    }
}

// PyYtException lives in Exceptions.cs (mirrors py_yt's PyYTSearchError base).
