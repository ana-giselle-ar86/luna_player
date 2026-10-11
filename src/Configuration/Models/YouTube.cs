using System.Text.Json.Serialization;

namespace LunaPlayer.Configuration.Models;

/// <summary>The picture heights the player offers. The backing number is the height in pixels, so it goes
/// straight to yt-dlp's height cap - except the two sentinels: <see cref="Best"/> (0) means no cap, take the
/// best, and <see cref="Worst"/> (-1) means take the smallest.</summary>
internal enum VideoQuality
{
    Worst = -1,
    P144 = 144, P240 = 240, P360 = 360, P480 = 480,
    P720 = 720, P1080 = 1080, P1440 = 1440, P2160 = 2160,
    Best = 0,
}

/// <summary>The audio bitrates the player offers. The backing number is the bitrate in kilobits per second,
/// which goes straight to yt-dlp's bitrate cap - except the two sentinels: <see cref="Best"/> (0) means no
/// cap, take the best, and <see cref="Worst"/> (-1) means take the smallest.</summary>
internal enum AudioQuality
{
    Worst = -1,
    Kbps64 = 64, Kbps96 = 96, Kbps128 = 128, Kbps160 = 160,
    Kbps192 = 192, Kbps256 = 256, Kbps320 = 320,
    Best = 0,
}
internal enum MixedLinkBehavior { Ask, Video, Playlist }
internal enum YtDlpChannel { Stable, Nightly, Master }

internal sealed class YouTubeSettings
{
    /// <summary>The picture height a video is played and prefetched at, when Enter plays the video.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<VideoQuality>))]
    public VideoQuality VideoQuality { get; set; } = VideoQuality.P720;

    /// <summary>The bitrate the sound is played and prefetched at, when Ctrl+Enter plays the audio.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AudioQuality>))]
    public AudioQuality AudioQuality { get; set; } = AudioQuality.Kbps256;

    /// <summary>How many search results to ask for.</summary>
    /// <remarks>
    /// A target rather than an exact number: YouTube answers a search in batches of its own choosing, so the
    /// player keeps taking batches until it has this many and stops on the first one that takes it past.
    /// </remarks>
    public int SearchResultCount { get; set; } = 50;

    /// <summary>Whether the search box offers live suggestions as the user types.</summary>
    /// <remarks>
    /// On by default. Each keystroke that settles fetches a short list of
    /// completions from YouTube, so somebody who would rather type undisturbed - or is on a slow or metered
    /// connection - can turn the network chatter off here.
    /// </remarks>
    public bool SearchSuggestions { get; set; } = true;

    /// <summary>What to do with a link that names a video and a playlist at once.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<MixedLinkBehavior>))]
    public MixedLinkBehavior MixedLink { get; set; } = MixedLinkBehavior.Ask;

    [JsonConverter(typeof(JsonStringEnumConverter<YtDlpChannel>))]
    public YtDlpChannel Channel { get; set; } = YtDlpChannel.Stable;

    public bool CheckComponentUpdates { get; set; }

    /// <summary>A Netscape-format cookie file yt-dlp is given with <c>--cookies</c>, or empty for none.</summary>
    /// <remarks>
    /// Mutually exclusive with <see cref="CookiesFromFirefox"/>: choosing a file clears the Firefox flag and
    /// vice versa, so only one cookie source is ever handed to yt-dlp.
    /// </remarks>
    public string CookiesPath { get; set; } = "";

    /// <summary>Whether yt-dlp is told to read cookies live from Firefox (<c>--cookies-from-browser
    /// firefox</c>) rather than from a saved file.</summary>
    public bool CookiesFromFirefox { get; set; }

    /// <summary>The defaults a download uses, under a <c>download</c> object of their own because they are a
    /// self-contained group that the download dialog and the direct download both read.</summary>
    public DownloadSettings Download { get; set; } = new();

    internal YouTubeSettings Copy()
    {
        // MemberwiseClone is shallow, so it would share the one Download object between the live settings and
        // the working copy the Preferences window edits - and edits would leak back even on Cancel. Deep-copy
        // that one reference member; everything else is a value type, an enum or a string.
        var copy = (YouTubeSettings)MemberwiseClone();
        copy.Download = Download.Copy();
        return copy;
    }

    internal void Apply(YouTubeSettings source)
    {
        VideoQuality = source.VideoQuality;
        AudioQuality = source.AudioQuality;
        SearchResultCount = source.SearchResultCount;
        SearchSuggestions = source.SearchSuggestions;
        MixedLink = source.MixedLink;
        Channel = source.Channel;
        CheckComponentUpdates = source.CheckComponentUpdates;
        CookiesPath = source.CookiesPath;
        CookiesFromFirefox = source.CookiesFromFirefox;
        Download.Apply(source.Download);
    }
}
