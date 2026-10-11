using System.Diagnostics;
using System.Globalization;
using LunaPlayer.Configuration.Models;

namespace LunaPlayer.YouTube.Download;

/// <summary>Runs one yt-dlp download, reporting progress as it goes and handing back the saved file or the
/// reason it failed. Standalone from the playback-oriented <see cref="Client.Runner"/>: it owns its own
/// process so it can read the machine-readable progress off both streams and wait with no overall timeout,
/// since a download is as long as the file is.</summary>
///
/// <remarks>
/// Waits on an external process and must run on a worker thread. FFmpeg comes from the child PATH that
/// <see cref="Utils.Start"/> sets, so the audio conversion works with no separate install.
/// </remarks>
internal sealed class DownloadEngine
{
    private const int ErrorTailLines = 12;

    private readonly Settings _settings;

    /// <param name="settings">Read live for the cookie source, so a change in preferences takes effect on the
    /// next download without rebuilding the engine.</param>
    internal DownloadEngine(Settings settings) => _settings = settings;

    /// <summary>Downloads the request's video, calling <paramref name="report"/> as bytes arrive. Returns the
    /// saved file on success or the failure reason; throws <see cref="OperationCanceledException"/> when the
    /// token is set.</summary>
    internal DownloadOutcome Run(DownloadRequest request, Action<DownloadProgress> report, CancellationToken token)
    {
        try
        {
            Directory.CreateDirectory(request.Folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return DownloadOutcome.Failed(exception.Message);
        }

        var arguments = DownloadArguments.Build(request);
        Utils.AddCookies(arguments, _settings.YouTube);
        arguments.Add(request.Url);

        using var process = Utils.Start(Utils.YtDlpPath, arguments);
        // Killed the moment the token is set rather than at the next line, since yt-dlp can sit a long time
        // on a slow link or a retry, and a Cancel that only answers when it next speaks does not work.
        using var abort = token.Register(() => Stop(process));

        var sync = new object();
        var finalPath = string.Empty;
        var errorTail = new List<string>();

        void Handle(string line)
        {
            if (line.Length == 0)
                return;
            lock (sync)
            {
                if (line.StartsWith(DownloadArguments.FileSentinel + "|", StringComparison.Ordinal))
                {
                    finalPath = line[(DownloadArguments.FileSentinel.Length + 1)..].Trim();
                    return;
                }
                if (TryParseProgress(line, out var progress))
                {
                    report(progress);
                    return;
                }
                errorTail.Add(line);
                if (errorTail.Count > ErrorTailLines)
                    errorTail.RemoveAt(0);
            }
        }

        // Progress may land on either stream depending on yt-dlp's build, so both are read and parsed the
        // same way; the final-path print comes on stdout and the complaints on stderr.
        var readingErrors = Task.Run(() =>
        {
            string? line;
            while ((line = process.StandardError.ReadLine()) is not null)
                Handle(line.Trim());
        }, CancellationToken.None);

        try
        {
            string? line;
            while ((line = process.StandardOutput.ReadLine()) is not null)
            {
                token.ThrowIfCancellationRequested();
                Handle(line.Trim());
            }
            // No timeout: a download is as long as the file is. Cancellation, not a clock, ends a stuck one.
            process.WaitForExit();
            // A killed process exits like any other, so a cancel is reported here rather than left to look
            // like a download that failed.
            token.ThrowIfCancellationRequested();
        }
        catch (Exception)
        {
            Stop(process);
            throw;
        }
        finally
        {
            readingErrors.Wait(TimeSpan.FromSeconds(2));
        }

        return process.ExitCode == 0
            ? DownloadOutcome.Saved(finalPath)
            : DownloadOutcome.Failed(Diagnostic(errorTail, process.ExitCode));
    }

    private static bool TryParseProgress(string line, out DownloadProgress progress)
    {
        progress = default;
        if (!line.StartsWith(DownloadArguments.ProgressSentinel + "|", StringComparison.Ordinal))
            return false;
        // Sentinel, status, downloaded, total, total-estimate, speed, eta.
        var parts = line.Split('|');
        if (parts.Length < 7)
            return false;
        var downloaded = ParseLong(parts[2]);
        var total = ParseLong(parts[3]);
        if (total <= 0)
            total = ParseLong(parts[4]);
        var percent = total > 0 ? Math.Clamp(downloaded * 100.0 / total, 0, 100) : 0;
        progress = new DownloadProgress(parts[1], downloaded, total, percent, ParseDouble(parts[5]), ParseDouble(parts[6]));
        return true;
    }

    /// <summary>A yt-dlp number field as a count of bytes. "NA" and anything unparseable read as 0.</summary>
    private static long ParseLong(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? (long)value : 0;

    private static double ParseDouble(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value : 0;

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

    /// <summary>The reason a failed run gives, preferring the last line that reads like a yt-dlp error.</summary>
    private static string Diagnostic(IReadOnlyList<string> errorTail, int exitCode)
    {
        var complaint = string.Empty;
        for (var index = errorTail.Count - 1; index >= 0; index--)
        {
            if (errorTail[index].StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                complaint = errorTail[index].Trim();
                break;
            }
        }
        if (complaint.Length == 0)
            complaint = errorTail.LastOrDefault(line => line.Length > 0)?.Trim() ?? string.Empty;
        if (complaint.Length == 0)
            return $"yt-dlp exited with code {exitCode}.";
        return complaint.Length <= 220 ? complaint : string.Concat(complaint.AsSpan(0, 220).TrimEnd(), "...");
    }
}

