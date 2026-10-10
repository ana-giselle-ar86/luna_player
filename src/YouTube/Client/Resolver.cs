using System.Globalization;
using System.Text.Json;
using LunaPlayer.Media;

namespace LunaPlayer.YouTube.Client;

/// <summary>Turns a video into something playable through yt-dlp, the only thing that can decipher a
/// signed stream address.</summary>
///
/// <remarks>
/// Attempts JSON metadata first, then a formatted direct URL, and finally an unrestricted direct URL.
/// Later attempts provide less control over the selected format. It waits for an external process and must
/// run on a worker thread.
/// </remarks>
internal sealed class Resolver
{
    private readonly Runner _runner;

    internal Resolver(Runner runner) => _runner = runner;

    /// <summary>Turns a video into something playable.</summary>
    internal ResolveOutcome Resolve(
        string watchUrl, YouTubeResult item, bool audioOnly, int quality, CancellationToken token)
    {
        // Deno is required as well as yt-dlp; without it, returned stream URLs can be severely throttled.
        if (!Utils.HasAll)
            return ResolveOutcome.Failed(ResolveFailure.MissingComponents);
        var format = Runner.Format(audioOnly, quality);
        var diagnostic = string.Empty;
        try
        {
            var full = _runner.Run(["--no-playlist", "--dump-single-json", "-f", format, watchUrl], token);
            if (Runner.RateLimited(full))
                return ResolveOutcome.Failed(ResolveFailure.RateLimited, full.Diagnostic);
            if (!full.Failed && Helpers.Parse(full.Lines) is JsonElement data)
            {
                var described = Helpers.Describe(data, watchUrl, item);
                if (Helpers.PickStream(data) is string address)
                    return Ready(described, address);
                item = described;
            }
            diagnostic = full.Diagnostic;

            var formatted = _runner.Run(["--no-playlist", "-g", "-f", format, watchUrl], token);
            if (Runner.RateLimited(formatted))
                return ResolveOutcome.Failed(ResolveFailure.RateLimited, formatted.Diagnostic);
            if (formatted.Lines.Count > 0)
                return Ready(item, formatted.Lines[0]);
            diagnostic = formatted.Diagnostic.Length > 0 ? formatted.Diagnostic : diagnostic;

            var bare = _runner.Run(["--no-playlist", "-g", watchUrl], token);
            if (Runner.RateLimited(bare))
                return ResolveOutcome.Failed(ResolveFailure.RateLimited, bare.Diagnostic);
            if (bare.Lines.Count > 0)
                return Ready(item, bare.Lines[0]);
            diagnostic = bare.Diagnostic.Length > 0 ? bare.Diagnostic : diagnostic;
        }
        catch (OperationCanceledException)
        {
            return ResolveOutcome.Cancelled;
        }
        catch (Exception failure)
        {
            return ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message);
        }
        return ResolveOutcome.Failed(ResolveFailure.NoStream, diagnostic);
    }

    private static ResolveOutcome Ready(YouTubeResult item, string address)
        => ResolveOutcome.Ok(new Resolved(
            item.Url.Length > 0 ? item : item with { Url = address },
            address,
            // yt-dlp is asked for one address, not a pair. Its format strings prefer the streams that
            // carry sound and picture together, so there is never a second one to go with it.
            null,
            ExpiryOf(address)));

    /// <summary>How long a resolved address is good for.</summary>
    ///
    /// <remarks>
    /// YouTube signs these addresses and states the deadline in the address itself, so it is read rather
    /// than guessed. A margin comes off it because playback starts some time after the resolve and the
    /// deadline applies to the request, not to the video; half an hour stands in when there is no
    /// <c>expire</c> to read, which is well inside the shortest lifetime YouTube is known to issue.
    /// </remarks>
    private static DateTimeOffset ExpiryOf(string url)
    {
        var margin = TimeSpan.FromMinutes(2);
        var stated = StatedExpiry(url);
        if (stated is not DateTimeOffset expiry)
            return DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30) - margin;
        return expiry - margin;
    }

    private static DateTimeOffset? StatedExpiry(string url)
    {
        if (!LinkValidator.TryGetHttpUrl(url, out var uri))
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
