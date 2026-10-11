using System.Globalization;
using LunaPlayer.Configuration.Models;

namespace LunaPlayer.YouTube.Download;

/// <summary>Turns a <see cref="DownloadRequest"/> into the yt-dlp arguments that depend only on the request -
/// the format selector, the output template, the embedding and SponsorBlock flags, and the machine-readable
/// progress and final-path templates the engine parses. The engine adds the cookie source and the URL.</summary>
internal static class DownloadArguments
{
    /// <summary>The prefix a progress line carries, so the engine can tell it from yt-dlp's other output.</summary>
    internal const string ProgressSentinel = "LUNA";

    /// <summary>The prefix the final-path line carries.</summary>
    internal const string FileSentinel = "LUNAFILE";

    internal static List<string> Build(DownloadRequest request)
    {
        var arguments = new List<string>
        {
            "--no-playlist",
            "--no-warnings",
            "--newline",
            "--force-overwrites",
            // Machine-readable progress rather than the human bar, so bytes, speed and ETA are read exactly.
            "--progress-template",
            $"download:{ProgressSentinel}|%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s",
            // Printed once the file is in its final place, so the engine learns the real saved name.
            "--print",
            $"after_move:{FileSentinel}|%(filepath)s",
            "-P", request.Folder,
            "-o", OutputTemplate(request.FileName),
        };
        if (request.ConcurrentFragments > 1)
            arguments.AddRange(["--concurrent-fragments", request.ConcurrentFragments.ToString(CultureInfo.InvariantCulture)]);

        if (request.Kind == DownloadKind.Audio)
        {
            arguments.AddRange(["-f", AudioSelector(request.AudioQuality)]);
            arguments.AddRange(["-x", "--audio-format", AudioFormatName(request.AudioFormat)]);
            // A bitrate only means something for the lossy formats, and Best/Worst leave the encode uncapped.
            if (request.AudioQuality is not (AudioQuality.Best or AudioQuality.Worst))
                arguments.AddRange(["--audio-quality", $"{(int)request.AudioQuality}K"]);
        }
        else
        {
            arguments.AddRange(["-f", VideoSelector(request.VideoQuality)]);
            arguments.AddRange(["--merge-output-format", ContainerName(request.Container)]);
        }

        if (request.EmbedMetadata) arguments.Add("--embed-metadata");
        if (request.EmbedThumbnail) arguments.Add("--embed-thumbnail");
        if (request.EmbedChapters) arguments.Add("--embed-chapters");
        if (request.EmbedSubtitles) arguments.Add("--embed-subs");
        if (request.SponsorBlockRemove) arguments.AddRange(["--sponsorblock-remove", "default"]);

        // yt-dlp finds FFmpeg on the child PATH that Utils.Start sets, but naming it as well is harmless and
        // keeps the conversion working even if the PATH prepend is ever changed.
        arguments.AddRange(["--ffmpeg-location", Utils.NativeDirectory]);
        if (Utils.DenoRuntime is string runtime)
            arguments.AddRange(["--js-runtimes", runtime]);
        return arguments;
    }

    /// <summary>The output name template: the user's name with yt-dlp's own placeholders disabled by doubling
    /// the percent sign, or yt-dlp's title template when no name was given.</summary>
    private static string OutputTemplate(string fileName)
        => string.IsNullOrEmpty(fileName)
            ? "%(title)s.%(ext)s"
            : fileName.Replace("%", "%%") + ".%(ext)s";

    /// <summary>The format selector for a video save: best video+audio capped at a height, falling back
    /// toward the best the video has. 0 means no cap (best), a negative cap means worst.</summary>
    private static string VideoSelector(VideoQuality quality)
        => (int)quality switch
        {
            0 => "bv*+ba/b",
            < 0 => "wv*+wa/w",
            var height => $"bv*[height<=?{height}]+ba/b[height<=?{height}]/bv*+ba/b",
        };

    /// <summary>The source format selector for an audio save: the best (or worst) audio, which the audio
    /// extraction step then converts. The chosen bitrate is applied by that step, not here.</summary>
    private static string AudioSelector(AudioQuality quality)
        => quality == AudioQuality.Worst ? "worstaudio/worst" : "bestaudio/best";

    private static string ContainerName(VideoContainer container)
        => container == VideoContainer.Mkv ? "mkv" : "mp4";

    private static string AudioFormatName(AudioFileFormat format)
        => format switch
        {
            AudioFileFormat.M4a => "m4a",
            AudioFileFormat.Opus => "opus",
            AudioFileFormat.Flac => "flac",
            AudioFileFormat.Wav => "wav",
            _ => "mp3",
        };
}
