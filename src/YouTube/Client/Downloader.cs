using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LunaPlayer.YouTube.Client;

/// <summary>Saves a video to disk through yt-dlp, and reads the qualities one offers so the picker can
/// only ever list what the download can honour.</summary>
///
/// <remarks>
/// Waits for an external process and must run on a worker thread. yt-dlp chooses the output name from its
/// template; progress is parsed from its line-oriented output.
/// </remarks>
internal sealed partial class Downloader
{
    private readonly Runner _runner;

    internal Downloader(Runner runner) => _runner = runner;

    /// <summary>Saves a video into <paramref name="folder"/>, reporting as the bytes arrive.</summary>
    /// <param name="report">The name being written, the bytes so far and the bytes expected. Called from
    /// the thread this runs on.</param>
    internal void Download(
        string watchUrl,
        string folder,
        bool audioOnly,
        int quality,
        Action<string, long, long> report,
        CancellationToken token,
        int? exactQuality = null)
    {
        var arguments = new List<string>
        {
            "--newline",
            "--progress",
            "--no-warnings",
            "--no-playlist",
        };
        // The quality picker's exact pick wins over the settings quality; both are plain numbers now - a
        // bitrate in kbps for sound, a picture height for video.
        var chosen = exactQuality ?? quality;
        if (audioOnly)
            arguments.AddRange(["-x", "--audio-format", "m4a", "--audio-quality", $"{chosen}K"]);
        else
            arguments.AddRange(["-f", Runner.VideoFormat(chosen)]);
        arguments.AddRange(["-o", Path.Combine(folder, "%(title)s.%(ext)s")]);
        if (Utils.DenoRuntime is string runtime)
            arguments.AddRange(["--js-runtimes", runtime]);
        arguments.Add(watchUrl);

        var name = string.Empty;
        var run = _runner.Run(arguments, token, line =>
        {
            if (Destination().Match(line) is { Success: true } destination)
                name = Path.GetFileName(destination.Groups["path"].Value.Trim());
            if (Progress().Match(line) is not { Success: true } progress)
                return;
            var percent = double.Parse(progress.Groups["pct"].Value, CultureInfo.InvariantCulture);
            var total = Bytes(progress.Groups["size"].Value);
            report(name, (long)(total * percent / 100.0), total);
        });
        if (run.Failed)
            throw new InvalidOperationException(run.Diagnostic);
    }

    /// <summary>The distinct qualities a video actually offers, best first, for the download picker.</summary>
    /// <remarks>
    /// Video mode returns the picture heights (720, 1080...); audio mode the audio bitrates in kbps. The
    /// numbers come straight from yt-dlp's own format list, so the picker can only ever offer what the video
    /// has. An empty list means yt-dlp said nothing usable, and the caller falls back to the settings
    /// quality rather than showing an empty picker.
    /// </remarks>
    internal IReadOnlyList<int> AvailableQualities(string watchUrl, bool audioOnly, CancellationToken token)
    {
        var run = _runner.Run(["--no-playlist", "--dump-single-json", "--no-warnings", watchUrl], token);
        if (Helpers.Parse(run.Lines) is not JsonElement data
            || !data.TryGetProperty("formats", out var formats)
            || formats.ValueKind != JsonValueKind.Array)
            return [];
        var found = new SortedSet<int>();
        foreach (var format in formats.EnumerateArray())
        {
            if (format.ValueKind != JsonValueKind.Object)
                continue;
            if (audioOnly)
            {
                // An audio format carries sound and no picture; its bitrate rounds to the nearest kbps.
                if (Helpers.Has(format, "acodec") && !Helpers.Has(format, "vcodec")
                    && Helpers.Number(format, "abr") is double abr && abr > 0)
                    found.Add((int)Math.Round(abr));
            }
            // A video format carries a picture; its height names the quality.
            else if (Helpers.Has(format, "vcodec") && Helpers.Number(format, "height") is double height && height > 0)
                found.Add((int)Math.Round(height));
        }
        // SortedSet is ascending; the picker wants the best first.
        return found.Reverse().ToArray();
    }

    /// <summary>A size as yt-dlp prints it - "12.34MiB", "1.2GB" - in bytes.</summary>
    private static long Bytes(string text)
    {
        var match = Size().Match(text.Trim());
        if (!match.Success)
            return 0;
        var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        var multiplier = match.Groups["unit"].Value.ToUpperInvariant() switch
        {
            "KB" => 1000L,
            "MB" => 1000L * 1000,
            "GB" => 1000L * 1000 * 1000,
            "TB" => 1000L * 1000 * 1000 * 1000,
            "KIB" => 1024L,
            "MIB" => 1024L * 1024,
            "GIB" => 1024L * 1024 * 1024,
            "TIB" => 1024L * 1024 * 1024 * 1024,
            _ => 1L,
        };
        return (long)(value * multiplier);
    }

    [GeneratedRegex(@"\[download\]\s+(?<pct>\d+(?:\.\d+)?)%\s+of\s+~?\s*(?<size>\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex Progress();

    /// <summary>Either line yt-dlp writes naming a file it is about to produce.</summary>
    /// <remarks>
    /// Both, because an audio-only save writes two. The download names the container it fetched - an mp4 -
    /// and the extraction step then names the m4a that is actually left on disk, which is the name the user
    /// is waiting for. Matching only the first reports a file that will not be there at the end.
    /// </remarks>
    [GeneratedRegex(@"^\[(?:download|ExtractAudio)\] Destination:\s*(?<path>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Destination();

    [GeneratedRegex(@"^(?<value>[0-9]+(?:\.[0-9]+)?)\s*(?<unit>[KMGT]?i?B)$", RegexOptions.IgnoreCase)]
    private static partial Regex Size();
}
