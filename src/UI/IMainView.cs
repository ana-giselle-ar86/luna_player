using LunaPlayer.Actions;
using LunaPlayer.Configuration;
using LunaPlayer.Favorites;
using LunaPlayer.Equalizer;
using LunaPlayer.Iptv;
using LunaPlayer.Playback;
using LunaPlayer.UI.Equalizer;
using LunaPlayer.YouTube;

namespace LunaPlayer.UI;

internal readonly record struct FileSelection(string Path, string Directory);
internal readonly record struct BookmarkListItem(string Id, string Name, string Position);
internal enum BookmarkManagementAction { Jump, Rename, Delete }
internal readonly record struct BookmarkManagementRequest(BookmarkManagementAction Action, string Id);
internal enum OpenedFilesAction { Jump, Information }
internal readonly record struct OpenedFilesRequest(OpenedFilesAction Action, int SelectedIndex);
internal readonly record struct UiOperation(bool Success, string Error = "");
internal readonly record struct AppUpdatePrompt(string CurrentVersion, string AvailableVersion, string Changes);

/// <summary>One subtitle track as the Subtitles menu shows it: the mpv track id and the label already worded
/// for the user. The "Off" entry is not one of these - the menu always carries it itself.</summary>
internal readonly record struct SubtitleMenuEntry(int Id, string Label);

/// <summary>One entry in a Recents submenu: the path to reopen and the numbered label already worded for the
/// user (such as "1. song.mp3 — C:\Music").</summary>
internal readonly record struct RecentMenuEntry(string Path, string Label);

/// <summary>Whether the user picked a recent item to open, or its kind's Clear command.</summary>
internal enum RecentAction { Open, Clear }

/// <summary>What the user chose in a Recents submenu: open <see cref="Path"/>, or clear the whole
/// <see cref="Kind"/> (then <see cref="Path"/> is empty).</summary>
internal readonly record struct RecentCommand(RecentAction Action, RecentKind Kind, string Path);

/// <summary>Which half of a link naming a video and a playlist at once the user meant.</summary>
internal enum YouTubeLinkKind { Video, Playlist }

/// <summary>What the user asked to search YouTube for, and which kind of result they filtered to.</summary>
/// <param name="Query">The words to search for, already trimmed.</param>
/// <param name="Filter">The row chosen in the search dialog's filter, 0-5: no filter, live, upload date,
/// view count, playlist, channels.</param>
internal readonly record struct YouTubeSearchRequest(string Query, int Filter);

/// <summary>Everything the search window needs beyond the widgets it builds for itself.</summary>
///
/// <remarks>
/// The window owns the debounce and the thread the suggestions are fetched on - that is presentation, and
/// keeps the network work off the UI thread without the action handler having to know how the box is laid
/// out - but it is handed the fetch itself and the words to announce rather than reaching for the client
/// or the screen reader, neither of which the UI layer knows about.
/// </remarks>
/// <param name="InitialQuery">What to put in the box before it opens; usually empty.</param>
/// <param name="SuggestionsEnabled">Whether the box offers live suggestions as the user types.</param>
/// <param name="FetchSuggestions">Fetches the words YouTube offers to finish a query. Called on a
/// background thread, so it may block; an empty list is a fine answer.</param>
/// <param name="AnnounceSuggestions">Speaks that suggestions have appeared, on the UI thread.</param>
internal sealed record YouTubeSearchPrompt(
    string InitialQuery,
    bool SuggestionsEnabled,
    Func<string, CancellationToken, IReadOnlyList<string>> FetchSuggestions,
    Action AnnounceSuggestions);

internal readonly record struct FavoriteListItem(string Id, string Name, string Type, string Link);
internal enum FavoriteAction { Open, Add, Edit, Remove }
internal readonly record struct FavoriteRequest(FavoriteAction Action, string Id);

/// <summary>What the user typed into the favourite editor, before anything has checked it.</summary>
internal readonly record struct FavoriteDraft(string Name, FavoriteKind Kind, string Link);

/// <summary>One row in the IPTV source manager: a saved source shown by name and kind.</summary>
/// <param name="Id">The store's id, carried back on the request so the handler knows which source was
/// chosen without the window knowing anything about the store.</param>
/// <param name="Name">What the source is called.</param>
/// <param name="Type">The kind of source, worded for the user ("Xtream Codes", "M3U web address"...).</param>
/// <param name="Detail">A second column giving the server or file the source points at.</param>
internal readonly record struct IptvSourceListItem(string Id, string Name, string Type, string Detail);
internal enum IptvSourceAction { Open, Add, Edit, Remove }
internal readonly record struct IptvSourceRequest(IptvSourceAction Action, string Id);

/// <summary>Everything the channel browser needs to open: the source's loaded channels and categories, and
/// a way to speak its own help. The browser filters the channels itself and answers with the index of the
/// one the user chose to play, or null when they closed it without playing.</summary>
/// <param name="Title">The window title, naming the source being browsed.</param>
/// <param name="Categories">The groups the channels fall into, for the category filter.</param>
/// <param name="Channels">Every channel the source holds, in load order.</param>
/// <param name="SpeakHelp">Speaks a line of help, for the F1 key, on the UI thread.</param>
/// <param name="Guide">The programme guide, when one was loaded, for the "Now" column and the on-demand
/// now-and-next announcement. Null when the source carried no guide.</param>
internal readonly record struct ChannelBrowserPrompt(
    string Title,
    IReadOnlyList<IptvCategory> Categories,
    IReadOnlyList<IptvChannel> Channels,
    Action<string> SpeakHelp,
    EpgGuide? Guide = null);

/// <summary>How the results window talks back to whoever opened it.</summary>
///
/// <remarks>
/// Everything the window can do except play a video is done through here, with the window still open. Only
/// playing one closes it, because only playing one replaces what the window is for. Copying an address,
/// opening a browser, saving a video: all of those leave the user where they were, on the row they were on,
/// which is the whole point of a list.
///
/// A window cannot speak and must not wait: the list is the only thing on screen while a page is fetched,
/// so a call that blocked would take the screen reader and the Escape key with it for the length of a web
/// request. So paging reports rather than asks, and the answer comes back through a callback on the UI
/// thread.
/// </remarks>
internal interface IYouTubeResultsFeed
{
    /// <summary>The user has moved to a row.</summary>
    void Selected(int index);

    /// <summary>Puts the address of a row on the clipboard, saying so.</summary>
    void CopyLink(int index);

    /// <summary>Shows a row in the web browser.</summary>
    void OpenInBrowser(int index);

    /// <summary>Shows the channel that published a row, refusing aloud when it named none.</summary>
    void OpenChannel(int index);

    /// <summary>Saves a row to a folder on this computer, asking which folder first.</summary>
    void Download(int index);

    /// <summary>Adds a row to the favourites, under its own title and address.</summary>
    void AddFavorite(int index);

    /// <summary>Switches a channel browser to another tab. Returns at once; <paramref name="replaced"/>
    /// runs later on the UI thread with the new tab's rows, or with null when the switch failed or was
    /// refused so the window can restore its tab selector.</summary>
    void SwitchTab(int tabIndex, Action<IReadOnlyList<YouTubeResult>?> replaced);

    /// <summary>Asks for the next page. Returns at once; <paramref name="appended"/> runs later on the UI
    /// thread, and is given an empty list when there is nothing more to come.</summary>
    void RequestMore(Action<IReadOnlyList<YouTubeResult>> appended);

    /// <summary>The window has gone, so a page still in flight should be dropped rather than delivered.
    /// </summary>
    void Close();
}

/// <summary>What the user chose from the results list: which row, and whether to play its picture or its
/// sound alone.</summary>
/// <remarks>
/// The mode is the whole point of returning a record rather than a bare index: Enter asks for the video and
/// Ctrl+Enter for the audio, and the session needs to know which before it resolves the row.
/// </remarks>
internal readonly record struct ResultChoice(int Index, PlayMode Mode);

internal sealed record YouTubeResultsPrompt(
    string Title,
    string Label,
    IReadOnlyList<YouTubeResult> Results,
    int SelectedIndex,
    IYouTubeResultsFeed Feed,
    /// <summary>The channel browser's tab names, or null for a search or playlist, which has no tabs.</summary>
    IReadOnlyList<string>? Tabs = null,
    /// <summary>Which tab is showing when the window opens.</summary>
    int SelectedTab = 0);
internal sealed record PrefsOps(
    string SettingsPath,
    string BookmarksPath,
    string SettingsFolder,
    Func<string, bool> ExportSettings,
    Func<string, PlayerSettings?> ImportSettings,
    Func<PlayerSettings?> ResetSettings,
    Func<string, bool> ExportBookmarks,
    Func<string, bool> ImportBookmarks,
    /// <summary>Why the last backup or restore failed, for the message that reports it.</summary>
    Func<string> LastBackupError,
    Func<bool> OpenSettingsFolder,
    Func<UiOperation> RegisterFiles,
    Func<UiOperation> UnregisterFiles,
    /// <summary>Fetches the programs a YouTube download needs. Nothing else on the settings page uses
    /// them, so this is the only way in.</summary>
    /// <summary>Fetches the programs yt-dlp needs, from the release line the page currently shows.
    /// Reports for itself, behind its own window, so there is nothing for the page to say afterwards.
    /// </summary>
    /// <remarks>
    /// The channel is passed in rather than read from the settings, because the settings still hold the
    /// old one: the page edits a copy and applies it when the window is accepted. Reading it there would
    /// fetch from whichever line was chosen last time, not the one on screen.
    /// </remarks>
    Action<YtDlpChannel> DownloadYouTubeComponents,
    Action<PlayerSettings> ApplyImmediate);

internal interface IProgressView : IDisposable
{
    /// <summary>Shows how far the job has got, as a percentage from nought to a hundred.</summary>
    /// <remarks>
    /// A percentage rather than a count, because a job can change what it is counting part way through - the
    /// folder scan counts files it has sized up and then files it has looked at, two different totals - and a
    /// bar told raw counts has no way to know that. It is also what a screen reader reads out.
    /// </remarks>
    void Update(int percent, string message);

    /// <summary>Moves a bar that has no figure behind it. Called on every tick of a job that cannot say how
    /// far through it is, so the window still shows that something is happening.</summary>
    void Pulse();

    /// <summary>Whether the user has pressed Cancel. A plain flag rather than something reported back out
    /// of Update, so a job with nothing new to report can still be cancelled.</summary>
    bool Cancelled { get; }
}

internal interface IMainView : IDisposable
{
    event Action<ActionId>? ActionRequested;

    /// <summary>The user has chosen an equalizer preset from the menu, or null to switch it off.</summary>
    /// <remarks>
    /// Not an <see cref="ActionId"/>, because which presets exist is not something the fixed action table
    /// can describe and no key is bound to any of them.
    /// </remarks>
    event Action<string?>? EqualizerPresetRequested;

    /// <summary>The user has chosen a subtitle track from the menu, or null for Off. Not an
    /// <see cref="ActionId"/>, for the same reason as <see cref="EqualizerPresetRequested"/>: which subtitle
    /// tracks exist changes per file.</summary>
    event Action<int?>? SubtitleTrackRequested;

    /// <summary>The user picked an item in a Recents submenu - to open, or to clear that kind. Not an
    /// <see cref="ActionId"/>, like the equalizer and subtitle entries: which recents exist changes as files
    /// are opened.</summary>
    event Action<RecentCommand>? RecentRequested;
    event Action? CloseRequested;

    /// <summary>Asked when Escape is pressed on the main window with no modifier. Returning true means it
    /// was dealt with, and the key goes no further.</summary>
    event Func<bool>? EscapePressed;

    nint NativeHandle { get; }
    void Show();
    void Close();
    void RestoreAndRaise();
    void SetPlaying(bool isPlaying);

    /// <summary>Sets the window title. A screen reader announces the foreground window when its title changes,
    /// so the caller sets it only when the title feature is on and the text has actually changed.</summary>
    void SetWindowTitle(string title);
    void SetMediaLoaded(bool loaded);
    void SetShuffleChecked(bool isChecked);
    void SetRepeatFileChecked(bool isChecked);
    void SetSilenceRemovalChecked(bool isChecked);

    /// <summary>Ticks the equalizer preset in force, or the Off item when <paramref name="presetId"/> is
    /// null.</summary>
    void SetEqualizerPreset(string? presetId);

    /// <summary>Builds the equalizer submenu again, for when a preset has been added, renamed or removed.
    /// </summary>
    void RebuildEqualizerMenu(IReadOnlyList<PresetEntry> presets, string? selected);

    /// <summary>Opens the band editor on one preset, giving back what was saved or null if it was not.
    /// </summary>
    EqualizerEditResult? EditEqualizerPreset(EqualizerEditContext context);

    /// <summary>Opens the window listing every preset, giving back whether anything changed in it.
    /// </summary>
    bool ManageEqualizerPresets(Library library);
    void SetEditState(bool hasLocalFile, bool hasMedia);
    void SetBookmarkState(bool enabled);
    void SetMarkState(bool currentMarked, bool allMarked);
    void SetMarkedActionsEnabled(bool enabled);
    void SetVideoOptionsEnabled(bool enabled);

    /// <summary>Fills the whole screen with the window, or restores it, ticking the menu item to match.</summary>
    void SetFullScreen(bool fullScreen);

    /// <summary>Whether the window is filling the screen right now.</summary>
    bool IsFullScreen { get; }

    /// <summary>Enables or greys out the full-screen menu item; on only while a real picture is playing.</summary>
    void SetFullScreenAvailable(bool available);

    /// <summary>Enables or greys out the audio-track commands; on only while the file has more than one
    /// audio track to switch between.</summary>
    void SetAudioTrackControlsEnabled(bool enabled);

    /// <summary>Rebuilds the Subtitles submenu's track list from the file's tracks, ticking the active one
    /// (or Off when <paramref name="activeId"/> is null). The fixed load/remember commands below are left in
    /// place. Called a moment after each file loads.</summary>
    void RebuildSubtitleMenu(IReadOnlyList<SubtitleMenuEntry> entries, int? activeId);

    /// <summary>Ticks or clears the "remember subtitle for this session" box.</summary>
    void SetSubtitleRemember(bool on);

    /// <summary>Rebuilds the three Recents submenus (files, folders, playlists) from newest-first entries
    /// whose labels are already numbered and worded. An empty kind shows a disabled placeholder.</summary>
    void RebuildRecentsMenu(
        IReadOnlyList<RecentMenuEntry> files,
        IReadOnlyList<RecentMenuEntry> folders,
        IReadOnlyList<RecentMenuEntry> playlists);

    /// <summary>Ticks the subtitle track being read, or the Off item when <paramref name="activeId"/> is null,
    /// without rebuilding the submenu - for when the choice changes but the track list has not.</summary>
    void SetSubtitleSelection(int? activeId);

    /// <summary>Enables or disables the whole Subtitles submenu - off when nothing is loaded, since there is
    /// no file to read or attach a subtitle to; on whenever a file is open, embedded subtitles or not,
    /// because one can always be loaded from a file or a URL.</summary>
    void SetSubtitleControlsEnabled(bool enabled);

    /// <summary>Asks for a subtitle file to load, or null when the user cancels.</summary>
    string? ChooseSubtitleFile();

    /// <summary>Asks for a subtitle's web address to load, or null when the user cancels or leaves it blank.</summary>
    string? PromptSubtitleUrl();

    /// <summary>Puts the recording menu into the state the recorder is in.</summary>
    void SetRecordingState(LunaPlayer.Recording.RecordingState state);
    FileSelection? ChooseFile(string initialDirectory);
    /// <param name="message">What the window asks for. Empty leaves the system's own wording, which is
    /// what the callers that are choosing "a folder" and nothing more particular want.</param>
    string? ChooseFolder(string initialDirectory, string message = "");
    string? PromptText(string message, string caption, string value = "");
    bool Confirm(string message, string caption);
    void ShowInfo(string message, string caption);
    void ShowWarning(string message, string caption);
    void ShowError(string message, string caption);
    void ShowAbout();
    /// <summary>Offers a newer Luna Player release and shows its change list. True means update now.</summary>
    bool OfferAppUpdate(AppUpdatePrompt prompt);
    double? ChooseTime(double duration, double elapsed);
    /// <summary>Opens the sleep-timer window, prefilled from the last-used choices and the current armed state.
    /// Null means the window was cancelled.</summary>
    SleepTimerDialogResult? ChooseSleepTimer(SleepTimerRequest initial, bool armed, string? statusText);
    int? ChooseAudioDevice(IReadOnlyList<string> descriptions, int selectedIndex);
    /// <summary>Shows the file's audio tracks to switch between, each already worded for the user. Null means
    /// the window was cancelled.</summary>
    int? ChooseAudioTrack(IReadOnlyList<string> labels, int selectedIndex);
    BookmarkManagementRequest? ManageBookmarks(IReadOnlyList<BookmarkListItem> bookmarks);
    /// <summary>Shows the list of loaded files. The names are asked for a row at a time rather than handed
    /// over up front, so a playlist of any size opens at once.</summary>
    /// <param name="nameAt">The name to show for one row, called only as that row is drawn.</param>
    OpenedFilesRequest? ChooseOpenedFile(int count, Func<int, string> nameAt, int selectedIndex);
    /// <param name="proportional">Whether the job can say how far through it is. A job that cannot gets a
    /// window with no bar on it, because a bar that never moves is worse than none.</param>
    /// <param name="detailed">Whether the job reports several lines at a time rather than one. A
    /// detailed window shows them in a read-only text area; the rest get a label.</param>
    IProgressView BeginProgress(string title, string message, bool proportional, bool detailed);
    void ShowTextInfo(string title, string text);
    /// <summary>Shows the text an uploader wrote under a video, in the purpose-built window that can open its
    /// links and save it. <paramref name="title"/> is the video's title, shown above the text.</summary>
    void ShowVideoDescription(string title, string description);
    /// <summary>Asks which half of a link naming a video and a playlist the user meant. Null when they
    /// backed out.</summary>
    YouTubeLinkKind? ChooseYouTubeLinkKind();
    /// <summary>Opens the search window and returns what the user asked to search for, or null when they
    /// closed it without searching.</summary>
    YouTubeSearchRequest? SearchYouTube(YouTubeSearchPrompt prompt);
    FavoriteRequest? ManageFavorites(IReadOnlyList<FavoriteListItem> favorites, string selectedId);
    FavoriteDraft? EditFavorite(string caption, FavoriteDraft value);
    /// <summary>Opens the IPTV source manager and returns what the user asked to do with which source, or
    /// null when they closed it. The manager reopens after add, edit and remove, like the favourites one.
    /// </summary>
    IptvSourceRequest? ManageIptvSources(IReadOnlyList<IptvSourceListItem> sources, string selectedId);
    /// <summary>Opens the add-or-edit form for an IPTV source, giving back what was typed or null if the
    /// user backed out. The form shows only the fields the chosen kind needs.</summary>
    IptvSourceDraft? EditIptvSource(string caption, IptvSourceDraft value);
    /// <summary>Opens the channel browser on a loaded source and returns the index into
    /// <see cref="ChannelBrowserPrompt.Channels"/> of the channel the user chose to play, or null when they
    /// closed it without playing anything.</summary>
    int? BrowseChannels(ChannelBrowserPrompt prompt);
    /// <summary>Opens the list of results and returns the row the user chose to play and whether they asked
    /// for its picture or its sound alone, or null when they closed it without playing anything. Everything
    /// else the window offers it does for itself, through <see cref="IYouTubeResultsFeed"/>, without closing.
    /// </summary>
    ResultChoice? ShowYouTubeResults(YouTubeResultsPrompt prompt);
    /// <summary>Asks which of the qualities a video actually offers it should be saved at. The numbers are
    /// picture heights for a video download and audio bitrates in kbps for an audio-only one. Returns the
    /// chosen number, or null when the user backed out.</summary>
    int? ChooseYouTubeQuality(IReadOnlyList<int> options, bool audioOnly);
    /// <summary>Offers to fetch the programs a YouTube download needs. True when the user accepted.</summary>
    bool OfferYouTubeComponents();
    /// <summary>Opens the window where recording is set up and run. It is modal, but closing it does not
    /// end a recording: the sources and the recorder outlive it.</summary>
    void ShowRecording(
        LunaPlayer.Recording.AudioCatalog catalog, LunaPlayer.Recording.RecordingSources sources, LunaPlayer.Recording.RecordingEngine engine);
    /// <summary>Opens the window where files are converted to another audio format and returns what the user
    /// chose to convert, or null if they closed it without starting. It is modal; the conversion the caller
    /// then runs from the request is shown behind <see cref="RunConversion"/>'s own window.</summary>
    /// <param name="initialFiles">Files to put in the list before the window opens, for the "Convert with
    /// Luna" verb, or null to open it empty.</param>
    LunaPlayer.Media.ConversionRequest? ShowMediaConverter(IReadOnlyList<string>? initialFiles = null);

    /// <summary>Runs a conversion behind the converter's progress window, which does not return until the
    /// batch has finished or the user stopped it. Gives back what the batch produced, or null when it was
    /// stopped.</summary>
    /// <param name="work">The batch. It is handed something to report progress to and a token set when the
    /// user stops it, and returns what it converted.</param>
    LunaPlayer.Media.ConversionOutcome? RunConversion(
        Func<Action<LunaPlayer.Media.ConversionProgress>, CancellationToken, LunaPlayer.Media.ConversionOutcome> work);
    /// <summary>Shows a read-only report of a batch that mostly worked: a summary line, then a scrollable list
    /// of the files that failed with their reasons. For the fully successful case a plain message box is used
    /// instead.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="message">The summary shown above the list.</param>
    /// <param name="details">The per-file failures, one block each.</param>
    void ShowConversionReport(string title, string message, string details);
    PlayerSettings? EditPreferences(PlayerSettings settings, PrefsOps operations, Action<string> speakHelp);
    void ApplyShortcuts(ShortcutManager shortcuts);
    /// <summary>Starts watching for the system-wide shortcuts in <paramref name="shortcuts"/>, replacing any
    /// set already being watched. Returns false when the system would not let the player watch the keyboard at
    /// all, which is worth telling the user about: no global shortcut will work.</summary>
    bool ApplyGlobalShortcuts(ShortcutManager shortcuts);
}
