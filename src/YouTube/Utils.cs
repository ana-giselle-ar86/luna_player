using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using PyYt;

namespace LunaPlayer.YouTube;

/// <summary>The shared helpers the YouTube modules lean on: where the bundled programs live and how to
/// start them, the canonical spelling of a watch URL, and the wording of a resolve failure.</summary>
///
/// <remarks>
/// Grouped here because each piece is used from more than one module and belongs to none of them in
/// particular. yt-dlp and Deno are kept beside the player rather than in its settings folder, where an
/// installed copy can find them without a search; neither is shipped, so everything about them answers "is
/// it there" before it answers anything else. Deno is here because yt-dlp needs a JavaScript engine to work
/// out how YouTube has signed a stream this week - without one the addresses it produces are throttled or
/// rejected - so the two are fetched and checked as a pair.
/// </remarks>
internal static partial class Utils
{
    // ---- where the bundled programs live ----

    /// <summary>The folder the programs are kept in: the one the player itself runs from.</summary>
    internal static string Directory { get; } = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    /// <summary>The folder containing libmpv, FFmpeg and the other native libraries shipped with Luna.</summary>
    internal static string NativeDirectory { get; } = Path.Combine(Directory, "lib");

    internal static string YtDlpPath { get; } = Path.Combine(Directory, "yt-dlp.exe");

    internal static string DenoPath { get; } = Path.Combine(Directory, "deno.exe");

    internal static bool HasYtDlp => File.Exists(YtDlpPath);

    internal static bool HasDeno => File.Exists(DenoPath);

    /// <summary>Whether both are present. Either one on its own is not enough to resolve reliably.
    /// </summary>
    internal static bool HasAll => HasYtDlp && HasDeno;

    /// <summary>The names of the ones that are not there, for a message that says what is being fetched.
    /// </summary>
    internal static IReadOnlyList<string> Missing
    {
        get
        {
            var missing = new List<string>(2);
            if (!HasYtDlp) missing.Add("yt-dlp");
            if (!HasDeno) missing.Add("Deno");
            return missing;
        }
    }

    /// <summary>What to pass yt-dlp so it uses the Deno beside it, or null when there is none.</summary>
    /// <remarks>
    /// The path is spelled with forward slashes: yt-dlp splits this
    /// argument on the colon after <c>deno</c>, and a Windows drive letter carries a colon of its own.
    /// </remarks>
    internal static string? DenoRuntime
        => HasDeno ? $"deno:{Path.GetFullPath(DenoPath).Replace('\\', '/')}" : null;

    /// <summary>Starts one of the programs with the player's executable and native-library folders on its
    /// PATH.</summary>
    ///
    /// <remarks>
    /// The folders are prepended rather than the environment left alone, because yt-dlp looks for its helper
    /// programs on PATH. This makes it use Luna's FFmpeg from <c>lib</c>, while still finding yt-dlp and Deno
    /// beside LunaPlayer.exe. Changing only the child's PATH rather than Luna's own leaves Luna itself
    /// alone.
    /// </remarks>
    internal static Process Start(string executable, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Directory,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        var bundledPath = $"{Directory}{Path.PathSeparator}{NativeDirectory}";
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        info.Environment["PATH"] = path.Length > 0
            ? $"{bundledPath}{Path.PathSeparator}{path}"
            : bundledPath;
        return Process.Start(info) ?? throw new InvalidOperationException($"Could not start {executable}.");
    }

    // ---- watch URLs ----

    /// <summary>The canonical watch page for a video id.</summary>
    internal static string WatchUrl(string id) => $"https://www.youtube.com/watch?v={id}";

    // A YouTube video id is exactly eleven of these characters and nothing else.
    [GeneratedRegex("^[a-zA-Z0-9_-]{11}$")]
    private static partial Regex VideoIdShape();

    /// <summary>The canonical watch URL for whatever a link points at, or null when it names no video.</summary>
    /// <remarks>
    /// PyYt's id parser returns non-YouTube input verbatim rather than refusing it, so its answer is held to
    /// the strict shape of a real id before it is trusted; anything else - a playlist link, a channel page,
    /// a bare word - yields null, which is the "this is not a video" the callers expect.
    /// </remarks>
    internal static string? Canonical(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return null;
        string id;
        try
        {
            id = PyYt.Video.GetVideoId(link);
        }
        catch
        {
            return null;
        }
        return VideoIdShape().IsMatch(id) ? WatchUrl(id) : null;
    }

    // ---- failure wording ----

    /// <summary>Turns a failure a worker reported into the sentence the user reads.</summary>
    /// <remarks>
    /// Here rather than at the point of failure because <c>Tr</c> may only be called on the UI thread, and
    /// the workers are not on it. The raw detail follows the sentence.
    /// </remarks>
    /// <param name="fallback">What to say when nothing more precise is known. Each job has its own
    /// wording for it - a search that failed and a video that failed are not the same news - which is why
    /// it is passed in rather than fixed here.</param>
    internal static string Describe(ResolveFailure failure, string detail, string fallback = "")
    {
        var message = failure switch
        {
            // Translators: Shown when a video is private, deleted, or never existed.
            ResolveFailure.Unavailable => Tr("This video is not available."),
            // Translators: Shown when YouTube has the video but will not serve it - age restricted, blocked
            // in this country, or paid for.
            ResolveFailure.Unplayable => Tr("YouTube will not play this video here."),
            // Translators: Shown when a video exists but offers nothing the player can play.
            ResolveFailure.NoStream => Tr("Could not resolve a playable stream."),
            // Translators: Shown when the programs YouTube playback needs are not installed. "yt-dlp" is a
            // program name and is not translated.
            ResolveFailure.MissingComponents => Tr("YouTube components are missing. Download them from the YouTube settings to play or download videos."),
            // Translators: Shown when YouTube is refusing requests from this computer for the time being.
            // "HTTP 429" is the numbered error it answers with and is not translated.
            ResolveFailure.RateLimited => Tr("YouTube returned HTTP 429 (Too Many Requests). Your IP may be temporarily rate-limited."),
            // Translators: Shown when the request never reached YouTube or its answer never arrived.
            ResolveFailure.Network => Tr("Could not reach YouTube. Check the network connection."),
            _ => fallback.Length > 0
                ? fallback
                // Translators: Shown when something went wrong that the player cannot explain more precisely.
                : Tr("Could not read this video."),
        };
        return detail.Length == 0
            ? message
            // Translators: Adds the technical reason under a message about YouTube. {message} is that
            // message and {details} is the reason, which is not translated.
            : TrFormat("{message}\nDetails: {details}", message, Short(detail));
    }

    /// <summary>The first line of a diagnostic, cut short. A stack trace in a message box helps nobody.
    /// </summary>
    private static string Short(string detail)
    {
        var line = detail.Split('\n')[0].Trim();
        return line.Length <= 220 ? line : string.Concat(line.AsSpan(0, 220).TrimEnd(), "...");
    }

    /// <summary>What a video that would not resolve is called, when nothing more precise is known.
    /// </summary>
    internal static string StreamFailed =>
        // Translators: Shown when a video could not be turned into something playable and nothing said why.
        Tr("Could not resolve YouTube stream.");
}
