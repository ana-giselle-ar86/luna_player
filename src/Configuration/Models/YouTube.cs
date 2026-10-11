using System.Text.Json.Serialization;

namespace LunaPlayer.Configuration.Models;

/// <summary>The picture heights the player offers. The backing number is the
/// height in pixels, so it goes straight to yt-dlp's height cap.</summary>
internal enum VideoQuality
{
    P144 = 144, P240 = 240, P360 = 360, P480 = 480,
    P720 = 720, P1080 = 1080, P1440 = 1440, P2160 = 2160,
}

/// <summary>The audio bitrates the player offers. The backing number is the
/// bitrate in kilobits per second.</summary>
internal enum AudioQuality { Kbps64 = 64, Kbps128 = 128, Kbps256 = 256 }
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

    internal YouTubeSettings Copy() => (YouTubeSettings)MemberwiseClone();

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
    }
}
