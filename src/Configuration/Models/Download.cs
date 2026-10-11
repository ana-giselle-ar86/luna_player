using System.Text.Json.Serialization;

namespace LunaPlayer.Configuration.Models;

/// <summary>Whether a download saves the picture or the sound alone.</summary>
internal enum DownloadKind { Video, Audio }

/// <summary>The container a downloaded video is muxed into. MP4 is the most widely playable; MKV holds
/// anything but is less universally supported.</summary>
internal enum VideoContainer { Mp4, Mkv }

/// <summary>The file format a downloaded audio track is written as. The backing name is the one yt-dlp's
/// <c>--audio-format</c> takes.</summary>
internal enum AudioFileFormat { Mp3, M4a, Opus, Flac, Wav }

/// <summary>The defaults that drive a direct download, and the values a normal download's dialog opens
/// prefilled with. Everything a download needs beyond the video itself lives here.</summary>
internal sealed class DownloadSettings
{
    /// <summary>Whether a download saves video or audio by default.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DownloadKind>))]
    public DownloadKind Kind { get; set; } = DownloadKind.Video;

    /// <summary>Where downloads are saved. Empty means the default under Documents, resolved on load.</summary>
    public string Folder { get; set; } = "";

    /// <summary>The picture height a video download is capped at; yt-dlp takes the best at or below it.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<VideoQuality>))]
    public VideoQuality VideoQuality { get; set; } = VideoQuality.P1080;

    /// <summary>The container a downloaded video is muxed into.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<VideoContainer>))]
    public VideoContainer Container { get; set; } = VideoContainer.Mp4;

    /// <summary>The format a downloaded audio track is converted to.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AudioFileFormat>))]
    public AudioFileFormat AudioFormat { get; set; } = AudioFileFormat.Mp3;

    /// <summary>The bitrate a downloaded audio track is converted at, for the lossy formats.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AudioQuality>))]
    public AudioQuality AudioQuality { get; set; } = AudioQuality.Kbps256;

    /// <summary>How many pieces of a file yt-dlp fetches at once (<c>--concurrent-fragments</c>). More can be
    /// faster on well-connected links; one is gentlest.</summary>
    public int ConcurrentFragments { get; set; } = 4;

    /// <summary>Whether the title, uploader and other tags are written into the file.</summary>
    public bool EmbedMetadata { get; set; } = true;

    /// <summary>Whether the video's thumbnail is embedded as cover art.</summary>
    public bool EmbedThumbnail { get; set; }

    /// <summary>Whether the video's chapter marks are written into the file.</summary>
    public bool EmbedChapters { get; set; }

    /// <summary>Whether available subtitles are embedded into the file.</summary>
    public bool EmbedSubtitles { get; set; }

    /// <summary>Whether SponsorBlock segments (sponsor reads, intros and the like) are cut out.</summary>
    public bool SponsorBlockRemove { get; set; }

    internal DownloadSettings Copy() => (DownloadSettings)MemberwiseClone();

    internal void Apply(DownloadSettings source)
    {
        Kind = source.Kind;
        Folder = source.Folder;
        VideoQuality = source.VideoQuality;
        Container = source.Container;
        AudioFormat = source.AudioFormat;
        AudioQuality = source.AudioQuality;
        ConcurrentFragments = source.ConcurrentFragments;
        EmbedMetadata = source.EmbedMetadata;
        EmbedThumbnail = source.EmbedThumbnail;
        EmbedChapters = source.EmbedChapters;
        EmbedSubtitles = source.EmbedSubtitles;
        SponsorBlockRemove = source.SponsorBlockRemove;
    }
}
