using System.Globalization;

namespace LunaPlayer.UI.YouTube;

/// <summary>Labels for the quality lists, shared by the preferences page and the download dialog. 0 = best, negative = worst.</summary>
internal static class QualityLabels
{
    internal static string AudioLabel(int kbps) => kbps switch
    {
        // Translators: Quality choice that takes the best the video offers, with no cap.
        0 => Tr("Best quality"),
        // Translators: Quality choice that takes the smallest/lowest the video offers.
        < 0 => Tr("Worst quality"),
        // Translators: One audio-bitrate choice. {rate} is a number of kilobits per second.
        _ => TrFormat("{rate} kbps", kbps.ToString(CultureInfo.InvariantCulture)),
    };

    internal static string VideoLabel(int height)
    {
        var number = height.ToString(CultureInfo.InvariantCulture);
        return height switch
        {
            // Translators: Quality choice that takes the best the video offers, with no cap.
            0 => Tr("Best quality"),
            // Translators: Quality choice that takes the smallest/lowest the video offers.
            < 0 => Tr("Worst quality"),
            // Translators: Description of the 144p video quality.
            144 => Tr("144p (very low quality)"),
            // Translators: Description of the 240p video quality.
            240 => Tr("240p (low quality)"),
            // Translators: Description of the 360p video quality.
            360 => Tr("360p (medium quality)"),
            // Translators: Description of the 480p video quality.
            480 => Tr("480p (medium quality)"),
            // Translators: Description of the 720p video quality (high definition).
            720 => Tr("720p (HD)"),
            // Translators: Description of the 1080p video quality (full high definition).
            1080 => Tr("1080p (Full HD)"),
            // Translators: Description of the 1440p video quality (2K).
            1440 => Tr("1440p (2K)"),
            // Translators: Description of the 2160p video quality (4K).
            2160 => Tr("2160p (4K)"),
            // Translators: A video quality with no set description. {height} is a number of pixels.
            _ => TrFormat("{height}p", number),
        };
    }
}
