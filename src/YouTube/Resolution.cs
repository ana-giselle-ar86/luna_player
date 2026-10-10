using PyYt;

namespace LunaPlayer.YouTube;

/// <summary>Why a video could not be turned into something playable.</summary>
///
/// <remarks>
/// An enum rather than a message, because resolving happens on a worker thread and <c>Tr</c> may only be
/// called on the UI thread. The words are chosen where the failure is reported, from this and the raw
/// detail beside it.
/// </remarks>
internal enum ResolveFailure
{
    None,
    /// <summary>The caller asked for the work to stop, or the session it belonged to ended.</summary>
    Cancelled,
    /// <summary>The video is gone, private, or was never there.</summary>
    Unavailable,
    /// <summary>The video exists but YouTube will not serve it: age-gated, region-locked, or paid for.
    /// </summary>
    Unplayable,
    /// <summary>The video exists and is playable, but it offers nothing this player can use.</summary>
    NoStream,
    /// <summary>The programs YouTube playback needs - yt-dlp and the JavaScript runtime it leans on - are
    /// not installed.</summary>
    MissingComponents,
    /// <summary>YouTube is refusing requests from this address for the time being.</summary>
    RateLimited,
    /// <summary>The request never reached YouTube, or its answer never arrived.</summary>
    Network,
    Unknown,
}

/// <summary>A video and the addresses that play it.</summary>
/// <param name="Item">The video itself, so a caller has its title without asking again.</param>
/// <param name="Url">What to open. The whole video when it carries its own sound, the picture alone when
/// it does not.</param>
/// <param name="AudioUrl">The sound, when <paramref name="Url"/> is picture only. YouTube stops serving
/// the two together above 360p, so anything better arrives as a pair.</param>
/// <param name="Expires">When these addresses stop working. They are signed and short-lived, which is why
/// a resolve cannot simply be kept.</param>
internal sealed record Resolved(YouTubeResult Item, string Url, string? AudioUrl, DateTimeOffset Expires)
{
    internal bool IsFresh => DateTimeOffset.UtcNow < Expires;
}

/// <summary>What came of a resolve: the streams, or the reason there are none.</summary>
internal readonly record struct ResolveOutcome(Resolved? Value, ResolveFailure Failure, string Detail = "")
{
    internal static ResolveOutcome Ok(Resolved value) => new(value, ResolveFailure.None);

    internal static ResolveOutcome Failed(ResolveFailure failure, string detail = "")
        => new(null, failure, detail);

    internal static ResolveOutcome Cancelled { get; } = new(null, ResolveFailure.Cancelled);
}

/// <summary>Turns whatever the metadata library threw into the neutral outcome the player reports.</summary>
/// <remarks>
/// PyYt draws a coarser set of distinctions than yt-dlp does. A cancellation the user asked for is told
/// apart from every other failure by the token, not the exception type, because a cancellation can arrive
/// carrying somebody else's token. Everything network-shaped - a refused request, a socket that dropped, a
/// rate-limit surfacing as an HTTP error - reads as Network; a page PyYt could not make sense of reads as
/// Unknown. Note there is no RateLimited here: that verdict now comes only from yt-dlp, which sees the
/// "429" text; a rate limit reaching PyYt looks like any other network failure.
/// </remarks>
internal static class FailureMapping
{
    internal static ResolveOutcome Explain(Exception failure, CancellationToken token) => failure switch
    {
        OperationCanceledException when token.IsCancellationRequested => ResolveOutcome.Cancelled,
        OperationCanceledException => ResolveOutcome.Failed(ResolveFailure.Network, failure.Message),
        VideoNotFoundError => ResolveOutcome.Failed(ResolveFailure.Unavailable, failure.Message),
        RequestError => ResolveOutcome.Failed(ResolveFailure.Network, failure.Message),
        HttpRequestException or IOException => ResolveOutcome.Failed(ResolveFailure.Network, failure.Message),
        ParsingError => ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message),
        PyYtException => ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message),
        _ => ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message),
    };
}
