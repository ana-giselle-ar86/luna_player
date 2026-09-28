using System.Net;

namespace PyYt;

/// <summary>
/// Process-wide session and token state, mirroring py_yt's core/session.py module globals
/// (visitor_data, po_token, po_token_verifier, the token lock, and the shared HTTP session).
/// </summary>
public static class Session
{
    private static readonly object Gate = new();
    private static HttpClient? _sharedClient;

    /// <summary>Cached InnerTube visitorData (py_yt: _visitor_data).</summary>
    public static string? VisitorData { get; set; }

    /// <summary>Cached proof-of-origin token (py_yt: _po_token). Stays null unless a verifier supplies it.</summary>
    public static string? PoToken { get; set; }

    /// <summary>
    /// Optional callback resolving (visitorData, poToken), mirroring py_yt's po_token_verifier.
    /// py_yt accepts tuple/dict/str returns; here the callback returns the resolved pair directly.
    /// </summary>
    public static Func<CancellationToken, Task<(string? VisitorData, string? PoToken)>>? PoTokenVerifier { get; set; }

    /// <summary>Serializes token resolution across concurrent requests (py_yt: _token_lock).</summary>
    public static SemaphoreSlim TokenLock { get; } = new(1, 1);

    /// <summary>Shared HTTP client used when a caller does not inject its own (py_yt: get_session).</summary>
    internal static HttpClient SharedClient
    {
        get
        {
            lock (Gate)
            {
                if (_sharedClient is not null) return _sharedClient;
                var handler = new HttpClientHandler();
                string? proxyUrl = Environment.GetEnvironmentVariable("PROXY_URL");
                if (!string.IsNullOrWhiteSpace(proxyUrl))
                {
                    handler.Proxy = new WebProxy(proxyUrl);
                    handler.UseProxy = true;
                }
                // Per-request timeouts are enforced with a CancellationTokenSource, so the shared
                // client itself must not impose one.
                _sharedClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
                return _sharedClient;
            }
        }
    }

    /// <summary>Disposes the shared client and clears cached tokens (py_yt: close_session).</summary>
    public static void CloseSession()
    {
        lock (Gate)
        {
            _sharedClient?.Dispose();
            _sharedClient = null;
        }
        VisitorData = null;
        PoToken = null;
    }
}
