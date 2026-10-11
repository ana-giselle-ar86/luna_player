namespace LunaPlayer.YouTube.Download;

/// <summary>One progress update parsed from yt-dlp's machine-readable progress line.</summary>
/// <param name="Status">yt-dlp's own word: "downloading", "finished", and so on.</param>
/// <param name="DownloadedBytes">Bytes written so far.</param>
/// <param name="TotalBytes">The total expected, or 0 when yt-dlp has not said yet.</param>
/// <param name="Percent">0 to 100, or 0 when the total is not yet known.</param>
/// <param name="Speed">Bytes per second, or 0 when unknown.</param>
/// <param name="Eta">Seconds left, or 0 when unknown.</param>
internal readonly record struct DownloadProgress(
    string Status,
    long DownloadedBytes,
    long TotalBytes,
    double Percent,
    double Speed,
    double Eta);

/// <summary>What a finished download produced: the saved file, or why it failed.</summary>
/// <param name="FilePath">The final path on disk, when it succeeded.</param>
/// <param name="Error">The reason, when it failed. Empty on success.</param>
internal readonly record struct DownloadOutcome(bool Success, string FilePath, string Error)
{
    internal static DownloadOutcome Saved(string filePath) => new(true, filePath, string.Empty);

    internal static DownloadOutcome Failed(string error) => new(false, string.Empty, error);
}
