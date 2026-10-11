using LunaPlayer.Configuration.Models;

namespace LunaPlayer.YouTube.Download;

/// <summary>One fully-decided download: the video to fetch and every choice the engine needs to save it.
/// A direct download builds this from the saved <see cref="DownloadSettings"/>; a normal download builds it
/// from the dialog the user filled in.</summary>
internal sealed record DownloadRequest
{
    /// <summary>The canonical watch URL of the video to save.</summary>
    public required string Url { get; init; }

    /// <summary>Whether to save the picture (with its sound) or the sound alone.</summary>
    public DownloadKind Kind { get; init; }

    /// <summary>The folder the file is written into.</summary>
    public required string Folder { get; init; }

    /// <summary>The file name without extension, or empty to let yt-dlp name it from the video's title.</summary>
    public string FileName { get; init; } = "";

    /// <summary>The picture height a video save is capped at (or Best/Worst).</summary>
    public VideoQuality VideoQuality { get; init; } = VideoQuality.P1080;

    /// <summary>The container a video save is muxed into.</summary>
    public VideoContainer Container { get; init; } = VideoContainer.Mp4;

    /// <summary>The format an audio save is converted to.</summary>
    public AudioFileFormat AudioFormat { get; init; } = AudioFileFormat.Mp3;

    /// <summary>The bitrate an audio save is converted at (ignored for the lossless formats and for
    /// Best/Worst).</summary>
    public AudioQuality AudioQuality { get; init; } = AudioQuality.Kbps256;

    /// <summary>How many pieces of the file to fetch at once.</summary>
    public int ConcurrentFragments { get; init; } = 1;

    public bool EmbedMetadata { get; init; }
    public bool EmbedThumbnail { get; init; }
    public bool EmbedChapters { get; init; }
    public bool EmbedSubtitles { get; init; }
    public bool SponsorBlockRemove { get; init; }

    /// <summary>The request a direct download makes: the saved defaults, aimed at one video.</summary>
    /// <param name="fileName">An optional name without extension; empty uses the video's title.</param>
    internal static DownloadRequest FromSettings(DownloadSettings settings, string url, string fileName = "")
        => new()
        {
            Url = url,
            Kind = settings.Kind,
            Folder = settings.Folder,
            FileName = fileName,
            VideoQuality = settings.VideoQuality,
            Container = settings.Container,
            AudioFormat = settings.AudioFormat,
            AudioQuality = settings.AudioQuality,
            ConcurrentFragments = settings.ConcurrentFragments,
            EmbedMetadata = settings.EmbedMetadata,
            EmbedThumbnail = settings.EmbedThumbnail,
            EmbedChapters = settings.EmbedChapters,
            EmbedSubtitles = settings.EmbedSubtitles,
            SponsorBlockRemove = settings.SponsorBlockRemove,
        };
}
