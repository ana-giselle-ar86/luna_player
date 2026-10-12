using LunaPlayer.Configuration;
using LunaPlayer.YouTube;
using Downloads = LunaPlayer.YouTube.Download;

namespace LunaPlayer.Application.Commands.YouTube;

internal sealed partial class YouTube
{
    /// <summary>Direct-downloads the current video with the saved settings.</summary>
    private void DirectDownload()
    {
        if (RequireVideo(out var watchUrl))
            DirectDownloadTo(watchUrl);
    }

    /// <summary>Opens the download dialog for the current video.</summary>
    private void Download()
    {
        if (RequireVideo(out var watchUrl))
            DownloadTo(watchUrl, _player.CurrentTitle ?? string.Empty);
    }

    /// <summary>Downloads a video with the saved settings.</summary>
    internal void DirectDownloadTo(string url)
    {
        if (!EnsureComponents(() => DirectDownloadTo(url)))
            return;
        Run(Downloads.Request.FromSettings(_settings.YouTube.Download, url));
    }

    /// <summary>Opens the download dialog for a video, then downloads what was chosen.</summary>
    internal void DownloadTo(string url, string title)
    {
        if (!EnsureComponents(() => DownloadTo(url, title)))
            return;
        var stem = string.IsNullOrWhiteSpace(title) ? string.Empty : Paths.SafeFileName(title);
        var defaults = Downloads.Request.FromSettings(_settings.YouTube.Download, url, stem);
        if (_view.Dialogs.ShowDownloadOptions(defaults) is { } request)
            Run(request);
    }
    /// <summary>Runs a download behind the progress window and shows the result with a Success or Error
    /// title. A download the user stopped says nothing.</summary>
    private void Run(Downloads.Request request)
    {
        var engine = new Downloads.Engine(_settings);
        var outcome = _view.Dialogs.RunDownload((report, token) => engine.Run(request, report, token));
        if (outcome is not { } result)
            return;
        if (result.Success)
            _view.Dialogs.ShowInfo(
                // Translators: Shown when a download finished.
                Tr("The download finished. The file was saved to your downloads folder."),
                // Translators: Title of the message shown when a download has finished.
                Tr("Success"));
        else
            _view.Dialogs.ShowError(
                result.Error.Length > 0
                    // Translators: Shown when a download failed. {details} is the technical reason from yt-dlp,
                    // which is not translated.
                    ? TrFormat("The download could not be completed.\n\nDetails: {details}", result.Error)
                    // Translators: Shown when a download failed and no reason was given.
                    : Tr("The download could not be completed. Check your internet connection and try again."),
                // Translators: Title of the message shown when a download could not finish.
                Tr("Error"));
    }

    /// <summary>Whether the yt-dlp programs are present; offers to fetch them and retries if not.</summary>
    private bool EnsureComponents(System.Action retry)
    {
        if (Backend.HasComponents)
            return true;
        return _components.Ensure(_settings.YouTube.Channel, retry)
            is LunaPlayer.YouTube.Components.Service.ComponentsState.Ready;
    }
}
