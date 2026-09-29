using LunaPlayer.Accessibility;
using LunaPlayer.Actions;
using LunaPlayer.Configuration;
using LunaPlayer.Media;
using LunaPlayer.Playback;
using LunaPlayer.UI;
using LunaPlayer.YouTube;
using WxSharp;

namespace LunaPlayer.Application.ActionHandlers;

/// <summary>The commands that play, describe and save videos from YouTube.</summary>
///
/// <remarks>
/// Only the commands are here. Anything that outlives one keypress - the list a search returned, what
/// comes after the video playing, what Escape goes back to - belongs to <see cref="YouTubeSessions"/>,
/// which this hands to and otherwise leaves alone.
///
/// The three commands that act on the video playing take its address from
/// <see cref="MediaPlayer.CurrentSource"/> rather than from its path. The path is a signed stream address
/// that expires and names no video a person could look at; the source is the watch page it came from.
/// </remarks>
internal sealed class YouTubeActions
{
    private readonly IMainView _view;
    private readonly MediaPlayer _player;
    private readonly PlayerSettings _settings;
    private readonly ISpeechOutput _speech;
    private readonly IClipboardService _clipboard;
    private readonly Backend _backend;
    private readonly YouTubeSessions _sessions;
    private readonly Components _components;
    private readonly IApplicationDispatcher _dispatcher;
    private readonly MediaGuard _guard;

    internal YouTubeActions(
        ActionRouter router,
        IMainView view,
        MediaPlayer player,
        PlayerSettings settings,
        ISpeechOutput speech,
        IClipboardService clipboard,
        Backend backend,
        YouTubeSessions sessions,
        Components components,
        IApplicationDispatcher dispatcher)
    {
        _view = view;
        _player = player;
        _settings = settings;
        _speech = speech;
        _clipboard = clipboard;
        _backend = backend;
        _sessions = sessions;
        _components = components;
        _dispatcher = dispatcher;
        _guard = new MediaGuard(player, speech);
        router.Register(ActionId.OpenYouTubeLink, OpenLink);
        router.Register(ActionId.SearchYouTube, Search);
        router.Register(ActionId.VideoDownload, Download);
        router.Register(ActionId.VideoDescription, ShowDescription);
        router.Register(ActionId.VideoCopyLink, CopyLink);
        router.Register(ActionId.UpdateYouTubeComponents, components.Update);
    }

    private void OpenLink()
    {
        var typed = _view.PromptText(
            // Translators: Asks the user for the address of the YouTube video or playlist they want to play.
            Tr("Enter a YouTube video or playlist link."),
            // Translators: Title of the window that asks for a YouTube address.
            Tr("Open YouTube Link"));
        if (typed is null)
            return;
        var link = typed.Trim();
        var info = LinkValidator.Parse(link);
        if (!info.IsHttp)
        {
            // Translators: Title of the message shown when what was typed is not a YouTube address.
            ShowError(Tr("The link must start with http or https."), Tr("Invalid link"));
            return;
        }
        if (!info.IsYouTube)
        {
            ShowError(
                // Translators: Shown when the address typed into Open YouTube Link is a web address but not a YouTube one.
                Tr("This is not a valid YouTube link."),
                // Translators: Title of the message shown when a YouTube address was expected and something else was given.
                Tr("Invalid YouTube link"));
            return;
        }
        if (info.Kind == LinkKind.Channel)
        {
            ShowError(
                // Translators: Shown when the address names a whole YouTube channel, which this window cannot open.
                Tr("Channel links are not supported here."), Tr("Invalid YouTube link"));
            return;
        }

        var kind = ChooseKind(info);
        if (kind is LinkKind.Playlist)
            _sessions.OpenPlaylist(link);
        else if (kind is LinkKind.Video)
            _sessions.PlayLink(link);
    }

    /// <summary>Which half of the link to use, when it names a video and a playlist at once.</summary>
    /// <remarks>
    /// Null means the user backed out. An address naming only one of the two answers for itself, and the
    /// setting can answer for the rest, so the window is opened only when there is really a question.
    /// </remarks>
    private LinkKind? ChooseKind(LinkInfo info)
    {
        // A YouTube address that names neither - the bare domain, a /watch with nothing after it - is
        // treated as a video, so it is refused in the words that name what was expected of it. Calling it
        // a playlist would refuse it in the wrong ones.
        if (!info.HasVideo && !info.HasPlaylist)
            return LinkKind.Video;
        if (!info.HasVideo)
            return LinkKind.Playlist;
        if (!info.HasPlaylist)
            return LinkKind.Video;
        return _settings.YouTube.MixedLink switch
        {
            MixedLinkBehavior.Video => LinkKind.Video,
            MixedLinkBehavior.Playlist => LinkKind.Playlist,
            _ => _view.ChooseYouTubeLinkKind() switch
            {
                YouTubeLinkKind.Video => LinkKind.Video,
                YouTubeLinkKind.Playlist => LinkKind.Playlist,
                _ => null,
            },
        };
    }

    private void Search()
    {
        var prompt = new YouTubeSearchPrompt(
            InitialQuery: string.Empty,
            SuggestionsEnabled: _settings.YouTube.SearchSuggestions,
            FetchSuggestions: _backend.Suggestions,
            AnnounceSuggestions: AnnounceSuggestions);
        if (_view.SearchYouTube(prompt) is not { } asked || asked.Query.Length == 0)
            return;
        RunSearch(asked.Query, asked.Filter);
    }

    /// <remarks>
    /// The programs are ensured here, not at the dialog: a search prefetches and plays its first result,
    /// which needs yt-dlp. When they are missing the offer is made and the search picked up again once they
    /// arrive; when it is declined nothing happens, which is the whole of "I would rather not install them".
    /// </remarks>
    private void RunSearch(string query, int filter)
    {
        if (!Backend.HasComponents
            && _components.Ensure(_settings.YouTube.Channel, () => RunSearch(query, filter))
                is not Components.ComponentsState.Ready)
            return;
        _sessions.Search(query, filter);
    }

    /// <summary>Speaks that suggestions have appeared under the search box, on the UI thread.</summary>
    /// <remarks>Without interrupting, so it does not talk over the word the user is still typing.</remarks>
    private void AnnounceSuggestions() =>
        _speech.Speak(
            // Translators: Spoken when the list of search suggestions appears under the YouTube search box.
            Tr("Search suggestions shown"),
            // Translators: The short wording spoken when search suggestions appear.
            Tr("Suggestions shown"), interrupt: false);

    private void Download()
    {
        if (RequireVideo(out var watchUrl))
            DownloadTo(watchUrl);
    }

    /// <remarks>
    /// Off the UI thread behind a progress window, because a download is as long as the file is. The job
    /// gets only the strings it needs; everything it reports is turned into words back on this thread.
    ///
    /// Saving goes through yt-dlp, the only thing that can turn a video into a file, so it needs the
    /// programs present. When they are not, the offer is made here rather than a refusal shown: accepting
    /// it fetches them and comes back to this.
    /// </remarks>
    internal void DownloadTo(string url)
    {
        if (!Backend.HasComponents
            && _components.Ensure(_settings.YouTube.Channel, () => DownloadTo(url))
                is not Components.ComponentsState.Ready)
            return;
        var folder = _view.ChooseFolder(
            _settings.General.LastDirectory,
            // Translators: Title of the window that asks where a video should be saved.
            Tr("Select download folder"));
        if (string.IsNullOrEmpty(folder))
            return;
        _settings.General.LastDirectory = folder;
        // A download saves the picture at the video quality. The audio-only setting that once governed this
        // is gone - playback now chooses picture or sound per play, but a saved file is a video - so the
        // picker below lists heights, and the sound is carried inside the file as yt-dlp muxes it.
        const bool audioOnly = false;
        var quality = (int)_settings.YouTube.VideoQuality;
        // Ask yt-dlp which qualities this video really offers, off-thread behind a short window, so the
        // picker only ever lists qualities the download can honour. It sweeps rather than fills, because a
        // single probe cannot say how far through it is.
        var probe = new ProgressPrompt(
            // Translators: Title of the short window shown while a video's available qualities are read.
            Tr("Reading qualities"),
            // Translators: Message shown while a video's available download qualities are being read.
            Tr("Reading available qualities..."),
            _ => Tr("Reading available qualities...")) { Proportional = false };
        BackgroundProgress.Start(_view, _dispatcher, probe,
            (report, token) => _backend.AvailableQualities(url, audioOnly, token),
            options => StartDownload(url, folder, audioOnly, quality, options));
    }

    /// <summary>Shows the quality picker, then saves the video at the chosen quality.</summary>
    /// <remarks>
    /// On the UI thread, once the probe has come back. A video that offered qualities gets the picker, and
    /// backing out of it abandons the download; one whose formats could not be read comes back with nothing
    /// to offer and is saved at the settings quality, exactly as it was before the picker existed.
    /// </remarks>
    private void StartDownload(
        string url, string folder, bool audioOnly, int quality, IReadOnlyList<int> options)
    {
        int? exactQuality = null;
        if (options.Count > 0)
        {
            var chosen = _view.ChooseYouTubeQuality(options, audioOnly);
            if (chosen is null)
                return;
            exactQuality = chosen;
        }
        var prompt = new ProgressPrompt(
            // Translators: Title of the window shown while a video is being saved to this computer.
            Tr("Downloading video"),
            // Translators: First message in the download window, before anything has arrived.
            Tr("Starting download..."),
            Describe) { Detailed = true };
        BackgroundProgress.Start(_view, _dispatcher, prompt,
            (report, token) => _backend.Download(url, folder, audioOnly, quality, report, token, exactQuality),
            Saved);
    }

    /// <summary>The lines the download window shows, as the Python player shows them.</summary>
    /// <remarks>
    /// Called from the progress window's own tick, which is on the UI thread, so it may translate. The
    /// name is only known once the first bytes arrive, so the heading stands alone until then rather than
    /// naming an empty file.
    /// </remarks>
    private static string Describe(Media.ProgressUpdate update)
    {
        var heading = update.Name.Length > 0
            // Translators: Progress heading while a video is being saved. {name} is the file being written.
            ? TrFormat("Downloading {name}", update.Name)
            // Translators: First message in the download window, before anything has arrived.
            : Tr("Starting download...");
        return heading + "\n" + Components.Sizes(update);
    }

    /// <remarks>
    /// Spoken rather than shown, in both directions, which is what the Python player does: a download runs
    /// for minutes behind a window the user has probably stopped looking at, and a message box that has to
    /// be dismissed before anything else works is the wrong way to say a file arrived.
    /// </remarks>
    private void Saved(YouTubeOutcome outcome)
    {
        if (outcome.Success)
        {
            _speech.Speak(
                // Translators: Spoken once a video has been saved to this computer.
                Tr("Download completed."),
                // Translators: The short wording spoken once a video has been saved.
                Tr("Download completed."));
            return;
        }
        var message = outcome.Error.Length > 0
            ? outcome.Error
            // Translators: Spoken when a video could not be saved and nothing said why.
            : Tr("Download failed.");
        _speech.Speak(message, message);
    }

    /// <remarks>
    /// The title goes above the text, as the Python player puts it there: the window is opened from a
    /// keystroke rather than from a list, so without it there is nothing saying which video this is about.
    /// </remarks>
    private void ShowDescription()
    {
        if (!RequireVideo(out var watchUrl))
            return;
        var title = _player.CurrentDisplayName ?? string.Empty;
        var prompt = new ProgressPrompt(
            // Translators: Title of the window shown while the text under a video is being fetched.
            Tr("Loading video details"),
            // Translators: Message shown while the text the uploader wrote under a video is being fetched.
            Tr("Loading video details..."),
            update => update.Name) { Proportional = false };
        BackgroundProgress.Start(_view, _dispatcher, prompt,
            (_, token) => _backend.Describe(watchUrl, token),
            found =>
            {
                if (found.Failure is ResolveFailure.Cancelled)
                    return;
                if (found.Text is null)
                {
                    ShowError(YouTubeSessions.Describe(found.Failure, found.Detail), Tr("YouTube"));
                    return;
                }
                var text = found.Text.Trim().Length > 0
                    ? found.Text.Trim()
                    // Translators: Shown in place of the text under a video when the uploader wrote none.
                    : Tr("No description is available.");
                _view.ShowVideoDescription(title, text);
            });
    }

    private void CopyLink()
    {
        if (RequireVideo(out var watchUrl))
            CopyToClipboard(watchUrl);
    }

    internal void CopyToClipboard(string url)
    {
        if (_clipboard.SetText(url))
            _speech.Speak(
                // Translators: Spoken once the address of a video has been put on the clipboard.
                Tr("Link copied."),
                // Translators: The short wording spoken once the address of a video has been copied.
                Tr("Copied."));
        else
            _speech.Speak(
                // Translators: Spoken when the address of a video could not be put on the clipboard.
                Tr("Could not copy link."),
                // Translators: The short wording spoken when the address of a video could not be copied.
                Tr("Copy failed."));
    }

    /// <remarks>
    /// Two attempts, as the Python player makes two: wxWidgets asks the system for the browser registered
    /// for http, and where nothing answers to that the shell is asked to open the address as it would from
    /// the Run box. The second catches a machine whose default browser is set but not associated.
    /// </remarks>
    internal void OpenInBrowser(string url)
    {
        if (Wx.LaunchDefaultBrowser(url) || Shell(url))
            return;
        _speech.Speak(
            // Translators: Spoken when the web browser could not be started to show a video.
            Tr("Could not open browser."),
            // Translators: The short wording spoken when the web browser could not be started.
            Tr("Browser open failed."));
    }

    private static bool Shell(string url)
    {
        try
        {
            using var opened = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    /// <summary>Whether what is playing came from YouTube, refusing aloud when it did not.</summary>
    /// <remarks>
    /// The three video commands are on a menu that is disabled unless this holds, so reaching them any
    /// other way means a shortcut was pressed. <see cref="MediaGuard"/> handles the case where nothing is
    /// loaded at all, so only the second half is worded here.
    /// </remarks>
    private bool RequireVideo(out string watchUrl)
    {
        watchUrl = string.Empty;
        if (!_guard.RequireFile(out _))
            return false;
        if (_player.CurrentSource is string source && LinkValidator.IsYouTubeUrl(source))
        {
            watchUrl = source;
            return true;
        }
        _speech.Speak(
            // Translators: Spoken when a command that only works on a YouTube video is used on something else.
            Tr("No YouTube video is active."),
            // Translators: The short wording spoken when a YouTube command is used on something that is not one.
            Tr("No YouTube video."));
        return false;
    }

    private void Report(YouTubeOutcome outcome)
    {
        if (!outcome.Success)
            ShowError(outcome.Error, Tr("YouTube"));
    }

    private void ShowError(string message, string caption) => _view.ShowError(message, caption);
}
