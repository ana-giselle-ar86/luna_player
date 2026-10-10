using LunaPlayer.Configuration;

namespace LunaPlayer.YouTube.Client;

/// <summary>Reads yt-dlp's version and has it replace itself with a newer build.</summary>
///
/// <remarks>
/// Uses yt-dlp's own updater because it can replace its running executable on Windows, which a plain
/// overwrite cannot. Waits for an external process and must run on a worker thread.
/// </remarks>
internal sealed class Updater
{
    private readonly Runner _runner;

    internal Updater(Runner runner) => _runner = runner;

    /// <summary>The version of the yt-dlp beside the player, or an empty string when there is none.
    /// </summary>
    internal string Version(CancellationToken token)
    {
        if (!Utils.HasYtDlp)
            return string.Empty;
        var run = _runner.Run(["--version"], token, timeout: TimeSpan.FromSeconds(60));
        return run.Failed || run.Lines.Count == 0 ? string.Empty : CleanVersion(run.Lines[0]);
    }

    /// <summary>Has yt-dlp replace itself with the newest build on a channel.</summary>
    /// <param name="report">Each line yt-dlp prints, so the window shows what it is doing.</param>
    internal (string Before, string After, bool Updated) SelfUpdate(
        YtDlpChannel channel, Action<string> report, CancellationToken token)
    {
        if (!Utils.HasYtDlp)
            throw new InvalidOperationException("yt-dlp is not available.");
        var before = Version(token);
        var name = ChannelName(channel);
        var run = _runner.Run(["--update-to", $"{name}@latest"], token, report, TimeSpan.FromMinutes(5));
        if (run.Failed)
            throw new InvalidOperationException(run.Diagnostic);
        var after = Version(token);
        var updated = after.Length > 0 && before.Length > 0 && !string.Equals(before, after, StringComparison.Ordinal);
        if (!updated)
        {
            // A first install through --update-to reports no version change, because there was no version
            // before it. Its own words are the only evidence that something happened.
            updated = run.Lines.Any(line =>
                line.Contains("Updated yt-dlp to", StringComparison.OrdinalIgnoreCase));
        }
        return (before, after.Length > 0 ? after : before, updated);
    }

    internal static string ChannelName(YtDlpChannel channel) => channel switch
    {
        YtDlpChannel.Nightly => "nightly",
        YtDlpChannel.Master => "master",
        _ => "stable",
    };

    /// <summary>The repository each channel is built from.</summary>
    internal static string ChannelRepository(YtDlpChannel channel) => channel switch
    {
        YtDlpChannel.Nightly => "yt-dlp/yt-dlp-nightly-builds",
        YtDlpChannel.Master => "yt-dlp/yt-dlp-master-builds",
        _ => "yt-dlp/yt-dlp",
    };

    /// <summary>Drops the program's own name from the front of a version string, as it prints it.</summary>
    private static string CleanVersion(string text)
    {
        var line = text.Trim();
        return line.StartsWith("yt-dlp", StringComparison.OrdinalIgnoreCase)
            ? line["yt-dlp".Length..].Trim(' ', ':', '-')
            : line;
    }
}
