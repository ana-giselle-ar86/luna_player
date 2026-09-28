using System.Globalization;

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

/// <summary>Works out how long a resolved address will last.</summary>
///
/// <remarks>
/// Choosing which stream to play is yt-dlp's job now - it is handed the same preferences the Python player
/// passes on the command line and returns the addresses already chosen - so all that is left here is
/// reading the deadline YouTube signs into those addresses.
/// </remarks>
internal static class StreamPicker
{
    /// <summary>How long a resolved address is good for.</summary>
    ///
    /// <remarks>
    /// YouTube signs these addresses and states the deadline in the address itself, so it is read rather
    /// than guessed. A margin comes off it because playback starts some time after the resolve and the
    /// deadline applies to the request, not to the video; half an hour stands in when there is no
    /// <c>expire</c> to read, which is well inside the shortest lifetime YouTube is known to issue.
    /// </remarks>
    internal static DateTimeOffset ExpiryOf(string url)
    {
        var margin = TimeSpan.FromMinutes(2);
        var stated = StatedExpiry(url);
        if (stated is not DateTimeOffset expiry)
            return DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) - margin;
        return expiry - margin;
    }

    /// <summary>The earlier of the deadlines two addresses state, so a pair is treated as one.</summary>
    internal static DateTimeOffset ExpiryOf(string url, string? audioUrl)
    {
        var first = ExpiryOf(url);
        return audioUrl is null ? first : first < ExpiryOf(audioUrl) ? first : ExpiryOf(audioUrl);
    }

    private static DateTimeOffset? StatedExpiry(string url)
    {
        if (!LunaPlayer.Media.LinkValidator.TryGetHttpUrl(url, out var uri))
            return null;
        var query = uri.Query;
        if (query.Length <= 1)
            return null;
        foreach (var pair in query[1..].Split('&'))
        {
            if (!pair.StartsWith("expire=", StringComparison.Ordinal))
                continue;
            if (long.TryParse(pair.AsSpan("expire=".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            return null;
        }
        return null;
    }
}
