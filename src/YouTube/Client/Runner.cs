using System.Diagnostics;
using LunaPlayer.Configuration;
using LunaPlayer.Configuration.Models;

namespace LunaPlayer.YouTube.Client;

/// <summary>What one run of yt-dlp produced.</summary>
/// <param name="Lines">Its output, blank lines dropped.</param>
/// <param name="Diagnostic">The first line of its complaint, when it failed. Empty when it did not.</param>
internal readonly record struct YtDlpRun(IReadOnlyList<string> Lines, string Diagnostic)
{
    internal bool Failed => Diagnostic.Length > 0;
}

/// <summary>Runs yt-dlp and hands back what it printed.</summary>
///
/// <remarks>
/// The one place a yt-dlp process is started. Everything the other pieces of this module do - resolving,
/// downloading, listing a channel, updating - is phrased as arguments and routed through <see cref="Run"/>,
/// so the common prefix, the Deno runtime and the cookie source are added in exactly one place. Its
/// operations wait for an external process and must run on a worker thread.
/// </remarks>
internal sealed class Runner
{
    private readonly Settings _settings;

    /// <param name="settings">Read live on each run, so a cookie source chosen in preferences takes effect
    /// on the next yt-dlp call without the runner being rebuilt.</param>
    internal Runner(Settings settings) => _settings = settings;

    /// <summary>The yt-dlp format selector for sound alone, capped at a bitrate in kbps. A cap of 0 means no
    /// cap (take the best) and a negative cap means take the worst.</summary>
    private static string AudioFormat(int abr)
        => abr switch
        {
            0 => "bestaudio[ext=m4a]/bestaudio/best",
            < 0 => "worstaudio/worst",
            _ => $"bestaudio[abr<=?{abr}][ext=m4a]/bestaudio[abr<=?{abr}]/bestaudio[ext=m4a]/bestaudio/best",
        };

    /// <summary>The yt-dlp format selector for a video capped at a picture height. A cap of 0 means no cap
    /// (take the best) and a negative cap means take the worst.</summary>
    internal static string VideoFormat(int height)
        => height switch
        {
            0 => "best[ext=mp4]/best",
            < 0 => "worst[ext=mp4]/worst",
            _ => $"best[height<=?{height}][ext=mp4]/best[height<=?{height}]/best[ext=mp4]/best",
        };

    /// <summary>The format selector for a play: sound alone capped at a bitrate, or picture capped at a
    /// height. The number is a bitrate in kbps when <paramref name="audioOnly"/>, a height otherwise.</summary>
    internal static string Format(bool audioOnly, int quality)
        => audioOnly ? AudioFormat(quality) : VideoFormat(quality);

    /// <summary>Whether a run was refused for making too many requests.</summary>
    internal static bool RateLimited(YtDlpRun run)
        => run.Diagnostic.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase)
            || run.Diagnostic.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);

    internal YtDlpRun Run(
        IEnumerable<string> arguments,
        CancellationToken token,
        Action<string>? onLine = null,
        TimeSpan? timeout = null)
    {
        var all = new List<string> { "--no-warnings", "--extractor-args", "youtube:player_client=android" };
        all.AddRange(arguments);
        // Only added when it is not already there: the download path puts it in itself, because it builds
        // its own argument list rather than going through the common prefix.
        if (Utils.DenoRuntime is string runtime && !all.Contains("--js-runtimes"))
            all.AddRange(["--js-runtimes", runtime]);
        Utils.AddCookies(all, _settings.YouTube);

        using var process = Utils.Start(Utils.YtDlpPath, all);
        // Killed the moment the token is set rather than at the next line of output. yt-dlp can sit for a
        // long time saying nothing - a slow site, a retry, a stalled connection - and a Cancel button that
        // only answers when the program next speaks is a Cancel button that does not work.
        using var abort = token.Register(() => Stop(process));
        var lines = new List<string>();
        var errors = new List<string>();
        // Read on a thread of its own. A program that fills one pipe while nothing drains the other stops
        // there for good, and yt-dlp writes a great deal to both.
        var reading = Task.Run(() =>
        {
            string? line;
            while ((line = process.StandardError.ReadLine()) is not null)
                errors.Add(line.Trim());
        }, CancellationToken.None);
        try
        {
            string? line;
            while ((line = process.StandardOutput.ReadLine()) is not null)
            {
                token.ThrowIfCancellationRequested();
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;
                onLine?.Invoke(trimmed);
                lines.Add(trimmed);
            }
            var span = timeout ?? TimeSpan.FromMinutes(3);
            if (!process.WaitForExit((int)span.TotalMilliseconds))
                throw new TimeoutException($"yt-dlp did not finish within {span.TotalSeconds:F0} seconds.");
            // A killed process exits like any other, so the abort is reported here rather than left to look
            // like a program that failed.
            token.ThrowIfCancellationRequested();
        }
        catch (Exception)
        {
            Stop(process);
            throw;
        }
        finally
        {
            reading.Wait(TimeSpan.FromSeconds(2));
        }
        if (process.ExitCode == 0)
            return new YtDlpRun(lines, string.Empty);
        return new YtDlpRun([], ShortDiagnostic(errors) is { Length: > 0 } complaint
            ? complaint
            : ShortDiagnostic(lines) is { Length: > 0 } fallback
                ? fallback
                : $"yt-dlp exited with code {process.ExitCode}.");
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // It finished between the test and the kill, which is the outcome that was wanted.
        }
    }

    private static string ShortDiagnostic(IReadOnlyList<string> lines)
    {
        var first = lines.FirstOrDefault(line => line.Length > 0)?.Trim() ?? string.Empty;
        return first.Length <= 220 ? first : string.Concat(first.AsSpan(0, 220).TrimEnd(), "...");
    }
}
