using LunaPlayer.Accessibility;
using LunaPlayer.Application;
using LunaPlayer.Configuration;
using LunaPlayer.Favorites;
using LunaPlayer.Media;
using LunaPlayer.Playback;
using LunaPlayer.UI;

namespace LunaPlayer.YouTube;

/// <summary>What came of asking for the video after this one.</summary>
///
/// <remarks>
/// Four states rather than three. A plain true/false/None cannot tell "the next video is on its way, do
/// not stop" from "there is nothing after this one, please stop"; answering both with false and stopping
/// in either case would end the session a moment before a still-resolving successor arrives.
/// </remarks>
internal enum NextOutcome
{
    /// <summary>What is playing is not part of a session, so the ordinary playlist rules apply.</summary>
    NotOurs,
    /// <summary>Playback has moved on already.</summary>
    Advanced,
    /// <summary>The next video is being resolved and will start itself. Do not stop.</summary>
    Pending,
    /// <summary>This was the last video.</summary>
    Exhausted,
}

/// <summary>Runs a list of YouTube videos: shows it, plays from it, moves through it and takes it away
/// again.</summary>
///
/// <remarks>
/// This is the part of the YouTube flow that could not live on
/// <see cref="Backend"/>: opening a video needs the player and the results window needs the session, and
/// <see cref="Backend"/> knows about neither. Everything here runs on the UI thread except the work handed
/// to <see cref="ResolveCache"/>, which is the only thing that touches the network.
/// </remarks>
internal sealed class YouTubeSessions : IDisposable
{
    /// <summary>How many videos to resolve ahead when a list of results is first shown.</summary>
    private const int OpeningPrefetch = 5;

    /// <summary>How many to resolve ahead when the user moves to a row.</summary>
    private const int BrowsingPrefetch = 2;

    private readonly IMainView _view;
    private readonly MediaPlayer _player;
    private readonly PlayerSettings _settings;
    private readonly ISpeechOutput _speech;
    private readonly IApplicationDispatcher _dispatcher;
    private readonly PyYtClient _client;
    private readonly Backend _backend;
    private readonly ResolveCache _cache;
    private readonly Action<string> _download;
    private readonly Action<string> _copy;
    private readonly Action<string> _browse;
    private readonly FavoriteStore _favorites;
    private readonly Components _components;
    private YouTubeSession? _session;
    private PendingNext? _pending;

    /// <param name="download">What to do when a row is downloaded. Saving a video is the action handler's
    /// job - it owns the folder chooser and the progress window - so it is passed in rather than repeated
    /// here.</param>
    /// <param name="favorites">The shared store, so a row can be saved to the favourites straight from the
    /// results list. The same store the favourites manager writes to, so an addition shows up there.</param>
    /// <param name="components">The programs the channel browser needs, offered when a channel is opened and
    /// they are not yet installed.</param>
    internal YouTubeSessions(
        IMainView view,
        MediaPlayer player,
        PlayerSettings settings,
        ISpeechOutput speech,
        IApplicationDispatcher dispatcher,
        PyYtClient client,
        Backend backend,
        ResolveCache cache,
        Action<string> download,
        Action<string> copy,
        Action<string> browse,
        FavoriteStore favorites,
        Components components)
    {
        _view = view;
        _player = player;
        _settings = settings;
        _speech = speech;
        _dispatcher = dispatcher;
        _client = client;
        _backend = backend;
        _cache = cache;
        _download = download;
        _copy = copy;
        _browse = browse;
        _favorites = favorites;
        _components = components;
    }

    /// <summary>The address of the video playing now, or null when what is playing is not one.</summary>
    internal string? CurrentWatchUrl => _player.CurrentSource;

    private string _activeChannelUrl = string.Empty;

    /// <summary>The channel address of the YouTube video playing now, or empty when it is not known - a
    /// video opened from a bare link carries no channel. Used by the video commands that open the channel.</summary>
    internal string ActiveChannelUrl => _activeChannelUrl;

    /// <summary>Searches YouTube and shows what it finds.</summary>
    ///
    /// <remarks>
    /// The first video is resolved before the window opens, which is worth doing: it means the row the list
    /// opens on plays the instant it is chosen, and it puts the first
    /// sign of a rate limit or a broken network in the progress window the user is already looking at
    /// rather than in a message box after they have picked something.
    /// </remarks>
    /// <param name="filter">The kind of result the user asked for in the search dialog, 0-5. It decides
    /// which YouTube search is run and whether the rows are videos, playlists, or channels.</param>
    internal void Search(string query, int filter)
    {
        var videoQuality = (int)_settings.YouTube.VideoQuality;
        var audioQuality = (int)_settings.YouTube.AudioQuality;
        var count = _settings.YouTube.SearchResultCount;
        // Read here and carried into the job: Tr may only be called on the thread that owns the windows,
        // and the job does not run on it.
        // Translators: Message shown while the first video of a search is being made ready to play.
        var fetching = Tr("Fetching first stream...");
        var prompt = new ProgressPrompt(
            // Translators: Title of the window shown while a YouTube search is running.
            Tr("Searching YouTube"),
            // Translators: First message shown while a YouTube search is running.
            Tr("Searching videos..."),
            update => update.Name) { Proportional = false };
        BackgroundProgress.Start(_view, _dispatcher, prompt,
            (report, token) => RunSearch(query, filter, count, videoQuality, audioQuality, fetching, report, token),
            found =>
            {
                if (found.Failure is ResolveFailure.Cancelled)
                    return;
                if (found.Failure is not ResolveFailure.None)
                {
                    ShowError(Describe(found.Failure, found.Detail,
                        // Translators: Shown when a YouTube search failed and nothing said why.
                        Tr("Could not complete YouTube search.")));
                    return;
                }
                if (found.Items.Count == 0)
                {
                    ShowError(
                        // Translators: Shown when a YouTube search found nothing at all.
                        Tr("No videos were found for this search."));
                    return;
                }
                Show(new YouTubeSession(
                    SessionKind.Search,
                    found.Items,
                    videoQuality,
                    audioQuality,
                    found.Page));
            });
    }

    /// <summary>Opens every video in a playlist and shows the list.</summary>
    internal void OpenPlaylist(string link)
    {
        if (!LinkValidator.Parse(link).HasPlaylist)
        {
            ShowError(
                // Translators: Shown when an address looked like a YouTube link but names no playlist.
                Tr("This link does not include a YouTube playlist."));
            return;
        }
        var videoQuality = (int)_settings.YouTube.VideoQuality;
        var audioQuality = (int)_settings.YouTube.AudioQuality;
        var prompt = new ProgressPrompt(
            // Translators: Title of the window shown while a YouTube playlist is being read.
            Tr("Loading YouTube link"),
            // Translators: First message shown while the videos in a YouTube playlist are being listed.
            Tr("Loading playlist items..."),
            update => update.Name) { Proportional = false };
        BackgroundProgress.Start(_view, _dispatcher, prompt,
            (_, token) => RunPlaylist(link, token),
            found =>
            {
                if (found.Failure is ResolveFailure.Cancelled)
                    return;
                if (found.Failure is not ResolveFailure.None)
                {
                    ShowError(Describe(found.Failure, found.Detail,
                        // Translators: Shown when a YouTube address could not be read and nothing said why.
                        Tr("Could not read YouTube link data.")));
                    return;
                }
                if (found.Items.Count == 0)
                {
                    ShowError(
                        // Translators: Shown when a YouTube playlist address opened nothing playable.
                        Tr("No videos were found in this playlist."));
                    return;
                }
                Show(new YouTubeSession(SessionKind.Playlist, found.Items, videoQuality, audioQuality));
            });
    }

    /// <summary>Opens a channel in the tabbed browser: its videos, shorts, streams, playlists and the rest,
    /// each paged as the user scrolls.</summary>
    /// <remarks>
    /// The browser is backed by yt-dlp, not PyYt, so it is gated on the programs being installed the same
    /// way a download is: when they are missing the offer is made and the channel opened again once they
    /// arrive. It opens on the videos tab; switching tabs, playing a video with next and previous, drilling
    /// into a playlist row and saving a row all flow through the same session machinery a search uses.
    /// </remarks>
    private void OpenChannel(YouTubeResult channel)
    {
        if (!Backend.HasComponents
            && _components.Ensure(_settings.YouTube.Channel, () => OpenChannel(channel))
                is not Components.ComponentsState.Ready)
            return;
        var videoQuality = (int)_settings.YouTube.VideoQuality;
        var audioQuality = (int)_settings.YouTube.AudioQuality;
        var channelBase = ChannelTabs.Normalise(channel.Url.Length > 0 ? channel.Url : channel.ChannelUrl);
        var tabIndex = ChannelTabs.DefaultIndex;
        var tabKey = ChannelTabs.Keys[tabIndex];
        var title = channel.Title;
        var prompt = new ProgressPrompt(
            // Translators: Title of the window shown while a YouTube channel is being opened.
            Tr("Loading channel"),
            // Translators: First message shown while a YouTube channel is being opened.
            Tr("Loading channel videos..."),
            update => update.Name) { Proportional = false };
        BackgroundProgress.Start(_view, _dispatcher, prompt,
            (_, token) => _backend.OpenChannelTab(channelBase, tabKey, token),
            found =>
            {
                if (found.Failure is ResolveFailure.Cancelled)
                    return;
                if (found.Failure is not ResolveFailure.None)
                {
                    ShowError(Describe(found.Failure, found.Detail,
                        // Translators: Shown when a YouTube channel could not be read.
                        Tr("Could not read this YouTube channel.")));
                    return;
                }
                // A channel whose videos tab is empty is still worth opening: the user can switch to a tab
                // that has something. Only a real failure stops it.
                Show(new YouTubeSession(
                    SessionKind.Channel, found.Items, videoQuality, audioQuality,
                    found.Page, channelBase, tabIndex, title));
            });
    }

    /// <summary>Adds a results row to the favourites, or removes it when it is already saved, saying which
    /// way it went.</summary>
    /// <remarks>
    /// The shared store, so it turns up in the favourites manager and survives a restart. A playlist row is
    /// saved as a playlist; everything else as a video, which is what a channel row's contents would be
    /// played as anyway. A row already saved - matched by its address - is taken back out, so the same key
    /// adds and removes. Success and failure are spoken rather than shown, because the results window is
    /// still up and a message box over it would take the caret off the row.
    /// </remarks>
    internal void ToggleFavorite(YouTubeResult item)
    {
        if (_favorites.FindByLink(item.Url) is Favorite existing)
        {
            if (_favorites.Delete(existing.Id))
            {
                _speech.Speak(
                    // Translators: Spoken once a video has been taken back out of the favourites from the results list.
                    Tr("Removed from favorites."),
                    // Translators: The short wording spoken once a video has been removed from the favourites.
                    Tr("Removed from favorites."));
                return;
            }
            var removeError = _favorites.LastError.Length > 0
                ? _favorites.LastError
                // Translators: Spoken when a video could not be removed from the favourites.
                : Tr("Could not remove from favorites.");
            _speech.Speak(removeError, removeError);
            return;
        }
        var kind = item.ItemType is YouTubeItemType.Playlist ? FavoriteKind.Playlist : FavoriteKind.Video;
        if (_favorites.Add(item.Title, kind, item.Url) is not null)
        {
            _speech.Speak(
                // Translators: Spoken once a video has been added to the favourites from the results list.
                Tr("Added to favorites."),
                // Translators: The short wording spoken once a video has been added to the favourites.
                Tr("Added to favorites."));
            return;
        }
        var message = _favorites.LastError.Length > 0
            ? _favorites.LastError
            // Translators: Spoken when a video could not be added to the favourites.
            : Tr("Could not add to favorites.");
        _speech.Speak(message, message);
    }

    /// <summary>Opens a video's channel in the player's own channel browser, from the channel address a row
    /// carried. The in-player counterpart of opening the channel in the web browser.</summary>
    internal void GoToChannel(string channelUrl, string title)
        => OpenChannel(new YouTubeResult(string.Empty, title, title, null, channelUrl, channelUrl));

    /// <summary>Plays one video, named by a link rather than chosen from a list.</summary>
    /// <remarks>There is no session: nothing follows a single video, so there is no next and Escape has
    /// nothing to go back to. Any session already open is closed.</remarks>
    internal void PlayLink(string link)
    {
        Clear();
        var watchUrl = PyYtClient.Canonical(link);
        if (watchUrl is null)
        {
            ShowError(
                // Translators: Shown when an address looked like a YouTube link but names no video.
                Tr("This link does not include a YouTube video."));
            return;
        }
        // A link is played as its picture, at the video quality: it is what Enter would do to a row, and a
        // lone link carries no key to say otherwise.
        Resolve(watchUrl, YouTubeResult.None, false, (int)_settings.YouTube.VideoQuality,
            CancellationToken.None,
            outcome =>
            {
                if (outcome.Value is Resolved ready)
                    OpenAlone(ready);
                else if (outcome.Failure is not ResolveFailure.Cancelled)
                    ShowError(Describe(outcome.Failure, outcome.Detail, StreamFailed));
            });
    }

    /// <summary>Moves to the video after the one playing, resolving it first if it is not ready.</summary>
    internal NextOutcome TryNext()
    {
        if (_session is not YouTubeSession session || CurrentWatchUrl is not string playing)
            return NextOutcome.NotOurs;
        var current = session.IndexOf(playing);
        if (current < 0)
            return NextOutcome.NotOurs;
        session.Selected = current;
        var next = current + 1;
        if (next >= session.Items.Count)
        {
            _pending = null;
            return NextOutcome.Exhausted;
        }
        var item = session.Items[next];
        if (Ready(session, item) is Resolved ready)
        {
            _pending = null;
            return Advance(session, current, next, ready) ? NextOutcome.Advanced : NextOutcome.Exhausted;
        }
        LoadNext(session, current, next, item);
        return NextOutcome.Pending;
    }

    /// <summary>Brings the session's idea of where the user is back in line with what is playing.
    /// </summary>
    ///
    /// <remarks>
    /// Moving through the playlist by the ordinary means can land on a session video without going through
    /// <see cref="TryNext"/> - pressing Previous does exactly that. Nothing about playback goes wrong when
    /// it does, but the row Escape returns to and the video counted as "the one after this" are both taken
    /// from here, so both would be a step behind.
    /// </remarks>
    internal void SyncSelection()
    {
        if (_session is not YouTubeSession session || CurrentWatchUrl is not string playing)
            return;
        var index = session.IndexOf(playing);
        if (index >= 0)
            session.Selected = index;
    }

    /// <summary>Answers Escape: stops a session video, takes the session's entries out of the playlist and
    /// puts the results list back.</summary>
    /// <returns>False when Escape means nothing here, so the key falls through to whatever else wants it.
    /// </returns>
    internal bool HandleEscape()
    {
        if (_session is not YouTubeSession session || CurrentWatchUrl is not string playing)
            return false;
        if (session.IndexOf(playing) < 0)
            return false;
        _pending = null;
        // The whole stage goes, list and all, and the playlist the user opened comes back exactly as it
        // was - paused where it was paused. There is nothing to unpick, because nothing was mixed in.
        _player.LeaveSession();
        Show(session);
        return true;
    }

    /// <summary>Forgets the current session and abandons everything it had running.</summary>
    internal void Clear()
    {
        _pending = null;
        _activeChannelUrl = string.Empty;
        var session = _session;
        _session = null;
        session?.Dispose();
        // Whatever the session was playing goes with it. Opening a file or a plain stream does this for
        // itself, so by the time a new one starts there is usually nothing left to take away.
        _player.LeaveSession();
    }

    public void Dispose() => Clear();

    // ---- showing the list ----

    /// <remarks>
    /// The session is installed before the window opens, not when something is first played. Installing it
    /// on first play would leave a first search with no session - so the page of
    /// results its "load more" fetches is dropped by the guard that checks the session is still the current
    /// one, after the continuation has already been consumed. Paging a fresh search therefore does nothing
    /// there and cannot be made to by trying again.
    /// </remarks>
    /// <remarks>
    /// Re-entrant, and has to be. The progress window is modeless, so the main window keeps taking
    /// commands while a search runs: the user can press Escape, land back in a list, and have the search
    /// they started arrive and open a second list over the top of it. When that happens the outer loop is
    /// left holding a session that is no longer the current one, and every iteration below re-checks that
    /// rather than carrying on with it - a session that has been replaced has already been disposed, and
    /// anything done to it from here would be done to something dead.
    /// </remarks>
    private void Show(YouTubeSession session)
    {
        if (!ReferenceEquals(_session, session))
        {
            Clear();
            _session = session;
        }
        while (ReferenceEquals(_session, session))
        {
            Prefetch(session, 0, OpeningPrefetch);
            using var feed = new Feed(this, session);
            // A channel session carries a tab bar; a search or playlist does not.
            var tabs = session.Kind is SessionKind.Channel ? ChannelTabs.DisplayNames() : null;
            var chosen = _view.ShowYouTubeResults(new YouTubeResultsPrompt(
                // Translators: Title of the window listing the videos a search or a playlist turned up.
                Tr("Videos"), session.Label, session.Items, session.Selected, feed, tabs, session.CurrentTab));
            // Another list opened over this one while it was up, and closing it took this session with
            // it. Whatever was chosen here belongs to a session that has gone.
            if (!ReferenceEquals(_session, session))
                return;
            if (chosen is not ResultChoice choice || choice.Index >= session.Items.Count)
            {
                Clear();
                return;
            }
            var index = choice.Index;
            // Which stream the user asked for - Enter for the picture, Ctrl+Enter for the sound - decides
            // what this play and every next after it resolves, so it is set on the session before anything
            // looks for a resolved address.
            session.Mode = choice.Mode;
            session.Selected = index;
            var item = session.Items[index];
            // The user asked to browse into this row's channel rather than play it. Handled like choosing a
            // channel row: the window has already closed, so opening the channel replaces this list cleanly.
            if (choice.Channel)
            {
                if (item.ChannelUrl.Length > 0)
                {
                    GoToChannel(item.ChannelUrl, item.Author);
                    return;
                }
                _speech.Speak(
                    // Translators: Spoken when the chosen video does not say which channel published it.
                    Tr("Channel link is not available."),
                    // Translators: The short wording spoken when the chosen video does not name its channel.
                    Tr("No channel link."));
                continue;
            }
            // A playlist or channel row is not played; choosing it browses one level in, replacing this
            // list with the videos of the playlist or the playlists of the channel, opening a fresh dialog
            // rather than trying to play the thing itself.
            if (item.ItemType is YouTubeItemType.Playlist)
            {
                OpenPlaylist(item.Url);
                return;
            }
            if (item.ItemType is YouTubeItemType.Channel)
            {
                OpenChannel(item);
                return;
            }
            // A video already resolved starts here and now, with no window in between. That is what the
            // prefetching is for, and it is the difference the user actually notices.
            if (Ready(session, item) is Resolved ready)
            {
                if (Start(session, index, ready))
                    return;
                // It would not open. Round the loop, which puts the list back on the same row.
                continue;
            }
            PlayAt(session, index, item);
            return;
        }
    }

    /// <summary>Resolves a video behind a progress window, then plays it. On any failure the results list
    /// comes back, so the user is never left with nothing open.</summary>
    private void PlayAt(YouTubeSession session, int index, YouTubeResult item)
    {
        if (!ReferenceEquals(_session, session))
            return;
        Resolve(item.Url, item, YouTubeSession.AudioFor(session.Mode), session.QualityFor(session.Mode),
            session.Token, outcome =>
        {
            if (!ReferenceEquals(_session, session))
                return;
            if (outcome.Value is Resolved ready && Start(session, index, ready))
                return;
            if (outcome.Failure is not (ResolveFailure.None or ResolveFailure.Cancelled))
                ShowError(Describe(outcome.Failure, outcome.Detail, StreamFailed));
            Show(session);
        });
    }

    private void Resolve(
        string watchUrl,
        YouTubeResult item,
        bool audioOnly,
        int quality,
        CancellationToken token,
        Action<ResolveOutcome> completed)
    {
        var prompt = new ProgressPrompt(
            // Translators: Title of the window shown while the address of a video is being looked up.
            Tr("Loading YouTube stream"),
            // Translators: First message shown while the address of a video is being looked up.
            Tr("Fetching stream URL..."),
            update => update.Name) { Proportional = false };
        BackgroundProgress.Start(_view, _dispatcher, prompt,
            (_, waitToken) => _cache.Wait(watchUrl, item, audioOnly, quality, token, waitToken),
            completed);
    }

    // ---- playing ----

    /// <summary>Starts a video from a session and makes it the session's current one.</summary>
    private bool Start(YouTubeSession session, int index, Resolved ready)
    {
        // Refusing rather than installing it: a session that is no longer the current one has been
        // disposed, and putting it back would leave the player driving a list nothing can add to.
        if (!ReferenceEquals(_session, session))
            return false;
        if (!Open(ready))
            return false;
        session.Selected = index;
        Prefetch(session, index + 1, 1);
        return true;
    }

    /// <summary>Starts a video from a list, in front of the playlist the user opened.</summary>
    /// <remarks>
    /// So that playlist keeps its files, its order and its place. Nothing here has to turn shuffle off
    /// either - the session's list starts without it, and the one the user set stays set on the playlist it
    /// belongs to.
    /// </remarks>
    private bool Open(Resolved ready)
    {
        _activeChannelUrl = ready.Item.ChannelUrl;
        return Report(_player.PlaySessionStream(
            ready.Url, ready.Item.Title, ready.Item.Url, ready.AudioUrl));
    }

    /// <summary>Starts a video that came from a link rather than from a list.</summary>
    /// <remarks>
    /// Into the playlist the user is working in, which is where a single video belongs: there is no list
    /// behind it to move through, nothing for Escape to go back to,
    /// and nothing a stage of its own would keep separate. A plain network stream is opened the same way.
    /// </remarks>
    private bool OpenAlone(Resolved ready)
    {
        _activeChannelUrl = ready.Item.ChannelUrl;
        return Report(_player.OpenStream(ready.Url, ready.Item.Title, ready.Item.Url, ready.AudioUrl));
    }

    private bool Report(bool opened)
    {
        if (opened)
            return true;
        ShowError(
            // Translators: Shown when a video was found but the player could not start playing it.
            Tr("Could not open YouTube stream."));
        return false;
    }

    /// <summary>Moves playback to a video that is ready, adding it to the playlist if it is not there yet.
    /// </summary>
    private bool Advance(YouTubeSession session, int from, int to, Resolved ready)
    {
        // Asked before anything is added to the playlist, not after. Between asking for the next video and
        // its arriving the user may have opened something else entirely - and an entry appended on the way
        // to a move that is then refused stays in the playlist for good, named after a video that is not
        // playing and pointing at an address that expires within the hour.
        if (!ReferenceEquals(_session, session)
            || CurrentWatchUrl is not string playing || session.IndexOf(playing) != from)
            return false;
        if (_player.IndexOfSource(ready.Item.Url) < 0
            && !_player.QueueSessionStream(ready.Url, ready.Item.Title, ready.Item.Url, ready.AudioUrl))
            return false;
        if (!_player.Next(wrap: false))
            return false;
        _activeChannelUrl = ready.Item.ChannelUrl;
        session.Selected = to;
        Prefetch(session, to + 1, 1);
        return true;
    }

    /// <summary>Resolves the next video in the background and moves to it when it arrives.</summary>
    private void LoadNext(YouTubeSession session, int from, int to, YouTubeResult item)
    {
        var pending = new PendingNext(session, from, to, item.Url);
        _pending = pending;
        _speech.Speak(
            // Translators: Spoken when the video after this one is being fetched before it can be played.
            Tr("Loading next video..."),
            // Translators: The short wording spoken while the video after this one is being fetched.
            Tr("Loading next video..."));
        var task = _cache.Start(
            _cache.Key(item.Url, YouTubeSession.AudioFor(session.Mode), session.QualityFor(session.Mode)),
            item.Url, item, YouTubeSession.AudioFor(session.Mode), session.QualityFor(session.Mode),
            session.Token);
        _ = task.ContinueWith(
            finished => _dispatcher.Post(() => NextReady(pending, finished.Result)),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    /// <remarks>
    /// The new title is announced, so the user is not left to work out what is playing. Something is also
    /// said when the video could not be fetched, so "Loading next video..." is not left as the last thing
    /// said with nothing following it.
    /// </remarks>
    private void NextReady(PendingNext pending, ResolveOutcome outcome)
    {
        if (!ReferenceEquals(_pending, pending) || !ReferenceEquals(_session, pending.Session))
            return;
        _pending = null;
        if (outcome.Value is not Resolved ready)
        {
            if (outcome.Failure is ResolveFailure.Cancelled)
                return;
            _speech.Speak(
                // Translators: Spoken when the video after this one could not be fetched, so playback stops.
                Tr("Could not load the next video."),
                // Translators: The short wording spoken when the next video could not be fetched.
                Tr("Next video failed."));
            return;
        }
        if (!Advance(pending.Session, pending.From, pending.To, ready))
            return;
        if (_settings.General.SpeakFileOnNavigation)
            _speech.Speak(ready.Item.Title, ready.Item.Title);
    }

    // ---- prefetching ----

    /// <summary>Starts resolving <paramref name="count"/> videos from <paramref name="start"/>, so they
    /// play without a wait when they are reached.</summary>
    /// <remarks>
    /// Both streams of each: the user chooses picture or sound only when they
    /// press the key, so both must be ready by then rather than one guessed at. The four-wide gate in
    /// <see cref="ResolveCache"/> keeps asking for two per row from flooding YouTube - the second simply
    /// queues behind the first - and a mode nobody plays costs one resolve that the bound soon evicts.
    /// </remarks>
    private void Prefetch(YouTubeSession session, int start, int count)
    {
        if (session.IsCancelled)
            return;
        var end = Math.Min(session.Items.Count, Math.Max(0, start) + count);
        for (var index = Math.Max(0, start); index < end; index++)
        {
            var item = session.Items[index];
            // Only videos resolve to a stream; a playlist or channel row is browsed into, not played, so
            // there is nothing to make ready for it.
            if (item.ItemType is not YouTubeItemType.Video)
                continue;
            _cache.Prefetch(item.Url, item, false, session.VideoQuality, session.Token);
            _cache.Prefetch(item.Url, item, true, session.AudioQuality, session.Token);
        }
    }

    private Resolved? Ready(YouTubeSession session, YouTubeResult item)
        => _cache.TryTake(
            _cache.Key(item.Url, YouTubeSession.AudioFor(session.Mode), session.QualityFor(session.Mode)));

    // ---- the background jobs ----

    private SearchResults RunSearch(
        string query,
        int filter,
        int count,
        int videoQuality,
        int audioQuality,
        string fetching,
        Action<ProgressUpdate> report,
        CancellationToken token)
    {
        try
        {
            var (items, page) = _client.Search(query, filter, count, token);
            if (items.Count == 0)
                return new SearchResults([], null, ResolveFailure.None, string.Empty);
            // Only a video is worth resolving ahead: a playlist or channel row opens a browse dialog, not a
            // stream, so a search filtered to those has nothing here to make ready.
            if (items[0].ItemType is YouTubeItemType.Video)
            {
                report(new ProgressUpdate(0, 0, fetching));
                // The sound is set going alongside, so Ctrl+Enter on the first row is ready too, while the
                // picture is waited for: the list opens on its first row played as video, and the point of
                // doing this before the window appears is that choosing that row plays at once. A video that
                // will not resolve is not an error - the user still gets their results - so the outcome is
                // dropped.
                _cache.Prefetch(items[0].Url, items[0], true, audioQuality, token);
                _ = _cache.Wait(items[0].Url, items[0], false, videoQuality, token, token);
            }
            return new SearchResults(items, page, ResolveFailure.None, string.Empty);
        }
        catch (Exception failure)
        {
            var explained = PyYtClient.Explain(failure, token);
            return new SearchResults([], null, explained.Failure, explained.Detail);
        }
    }

    private PlaylistResults RunPlaylist(string link, CancellationToken token)
    {
        var (title, items, failure, detail) = _backend.Playlist(link, token);
        return new PlaylistResults(title, items, failure, detail);
    }

    // ---- wording ----

    /// <summary>Turns a failure a worker reported into the sentence the user reads.</summary>
    /// <remarks>
    /// Here rather than at the point of failure because <c>Tr</c> may only be called on the UI thread, and
    /// the workers are not on it. The raw detail follows the sentence.
    /// </remarks>
    /// <param name="fallback">What to say when nothing more precise is known. Each job has its own
    /// wording for it - a search that failed and a video that failed are not the same news - which is why
    /// it is passed in rather than fixed here.</param>
    internal static string Describe(ResolveFailure failure, string detail, string fallback = "")
    {
        var message = failure switch
        {
            // Translators: Shown when a video is private, deleted, or never existed.
            ResolveFailure.Unavailable => Tr("This video is not available."),
            // Translators: Shown when YouTube has the video but will not serve it - age restricted, blocked
            // in this country, or paid for.
            ResolveFailure.Unplayable => Tr("YouTube will not play this video here."),
            // Translators: Shown when a video exists but offers nothing the player can play.
            ResolveFailure.NoStream => Tr("Could not resolve a playable stream."),
            // Translators: Shown when the programs YouTube playback needs are not installed. "yt-dlp" is a
            // program name and is not translated.
            ResolveFailure.MissingComponents => Tr("YouTube components are missing. Download them from the YouTube settings to play or download videos."),
            // Translators: Shown when YouTube is refusing requests from this computer for the time being.
            // "HTTP 429" is the numbered error it answers with and is not translated.
            ResolveFailure.RateLimited => Tr("YouTube returned HTTP 429 (Too Many Requests). Your IP may be temporarily rate-limited."),
            // Translators: Shown when the request never reached YouTube or its answer never arrived.
            ResolveFailure.Network => Tr("Could not reach YouTube. Check the network connection."),
            _ => fallback.Length > 0
                ? fallback
                // Translators: Shown when something went wrong that the player cannot explain more precisely.
                : Tr("Could not read this video."),
        };
        return detail.Length == 0
            ? message
            // Translators: Adds the technical reason under a message about YouTube. {message} is that
            // message and {details} is the reason, which is not translated.
            : TrFormat("{message}\nDetails: {details}", message, Short(detail));
    }

    /// <summary>The first line of a diagnostic, cut short. A stack trace in a message box helps nobody.
    /// </summary>
    private static string Short(string detail)
    {
        var line = detail.Split('\n')[0].Trim();
        return line.Length <= 220 ? line : string.Concat(line.AsSpan(0, 220).TrimEnd(), "...");
    }

    /// <summary>What a video that would not resolve is called, when nothing more precise is known.
    /// </summary>
    private static string StreamFailed =>
        // Translators: Shown when a video could not be turned into something playable and nothing said why.
        Tr("Could not resolve YouTube stream.");

    private void ShowError(string message) =>
        // Translators: Title of the messages the player shows about YouTube.
        _view.ShowError(message, Tr("YouTube"));

    private readonly record struct SearchResults(
        IReadOnlyList<YouTubeResult> Items, SearchPage? Page, ResolveFailure Failure, string Detail);

    private readonly record struct PlaylistResults(
        string Title, IReadOnlyList<YouTubeResult> Items, ResolveFailure Failure, string Detail);

    /// <summary>The move to the next video that is waiting on a resolve.</summary>
    private sealed record PendingNext(YouTubeSession Session, int From, int To, string WatchUrl);

    /// <summary>The results window's way of talking back: it reports where the user is, asks for more rows
    /// and says when it has gone.</summary>
    ///
    /// <remarks>
    /// One of these per opening of the window rather than one per session, so closing it cannot silence
    /// the next one. Every method runs on the UI thread - the page arrives through
    /// <see cref="IApplicationDispatcher.Post"/> - which is why one plain field is guard enough and no
    /// lock appears here.
    /// </remarks>
    private sealed class Feed : IYouTubeResultsFeed, IDisposable
    {
        private readonly YouTubeSessions _owner;
        private readonly YouTubeSession _session;
        private bool _closed;
        private bool _loading;

        internal Feed(YouTubeSessions owner, YouTubeSession session)
        {
            _owner = owner;
            _session = session;
        }

        public void Selected(int index) => _owner.Prefetch(_session, index, BrowsingPrefetch);

        public void CopyLink(int index) => On(index, item => _owner._copy(item.Url));

        public void OpenInBrowser(int index) => On(index, item => _owner._browse(item.Url));

        public void OpenChannelInBrowser(int index) => On(index, item => WithChannel(item, _owner._browse));

        /// <summary>Runs <paramref name="action"/> on the row's channel address, or says so when the listing
        /// named no channel. Shared by the browser and in-player ways of opening a channel.</summary>
        private void WithChannel(YouTubeResult item, Action<string> action)
        {
            if (item.ChannelUrl.Length > 0)
            {
                action(item.ChannelUrl);
                return;
            }
            _owner._speech.Speak(
                // Translators: Spoken when the chosen video does not say which channel published it.
                Tr("Channel link is not available."),
                // Translators: The short wording spoken when the chosen video does not name its channel.
                Tr("No channel link."));
        }

        public void Download(int index) => On(index, item => _owner._download(item.Url));

        public bool IsFavorite(int index) =>
            Peek(index) is YouTubeResult item && _owner._favorites.FindByLink(item.Url) is not null;

        public void ToggleFavorite(int index) => On(index, item => _owner.ToggleFavorite(item));

        /// <summary>Reads one row without making it the current selection - a query the context menu runs
        /// before it opens, which must not move the caret the way acting on a row does.</summary>
        private YouTubeResult? Peek(int index)
            => _closed || index < 0 || index >= _session.Items.Count ? null : _session.Items[index];

        /// <summary>Runs something on one row, so long as the row is still there.</summary>
        /// <remarks>
        /// The list can be longer than it was when the window opened - a page may have arrived while the
        /// user was reading - but never shorter, so this only has to guard the bounds rather than re-read
        /// what the row holds.
        /// </remarks>
        private void On(int index, Action<YouTubeResult> action)
        {
            if (_closed || index < 0 || index >= _session.Items.Count)
                return;
            _session.Selected = index;
            action(_session.Items[index]);
        }

        public void RequestMore(Action<IReadOnlyList<YouTubeResult>> appended)
        {
            if (_closed || _loading || _session.Exhausted || _session.IsCancelled)
                return;
            if (_session.Page is not IResultPage page || !page.HasMore)
            {
                _session.Exhausted = true;
                appended([]);
                return;
            }
            _loading = true;
            _owner._speech.Speak(
                // Translators: Spoken when the user reaches the end of the results list and more are being fetched.
                Tr("Loading more videos..."),
                // Translators: The short wording spoken while more search results are being fetched.
                Tr("Loading more videos..."));
            var count = _owner._settings.YouTube.SearchResultCount;
            var token = _session.Token;
            _ = Task.Run(() => page.Take(count, token), token).ContinueWith(
                finished => _owner._dispatcher.Post(() => Arrived(finished, appended)),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        public void Close() => _closed = true;

        public void Dispose() => Close();

        /// <summary>Loads another tab of the channel and swaps the list to it.</summary>
        /// <remarks>
        /// A tab already visited is switched to at once, with no fetch: the session kept its rows and its
        /// paging the first time, so returning to it is free. A tab not yet seen is fetched off the UI
        /// thread the way <see cref="RequestMore"/> pages, and its result posted back. <paramref
        /// name="replaced"/> is handed the rows on success, or null when the switch failed, was refused, or
        /// the window closed under it - so a deferred answer that arrives after the dialog has gone touches
        /// nothing that has been disposed. Only a channel session has tabs; anything else answers null at
        /// once.
        /// </remarks>
        public void SwitchTab(int tabIndex, Action<IReadOnlyList<YouTubeResult>?> replaced)
        {
            if (_closed || _session.IsCancelled
                || _session.Kind is not SessionKind.Channel
                || tabIndex < 0 || tabIndex >= ChannelTabs.Keys.Length)
            {
                replaced(null);
                return;
            }
            // Seen before: make it current and hand back its rows without a fetch.
            if (_session.HasTab(tabIndex))
            {
                _session.ActivateTab(tabIndex);
                replaced(_session.Items);
                _owner.Prefetch(_session, 0, OpeningPrefetch);
                return;
            }
            if (_loading)
            {
                replaced(null);
                return;
            }
            _loading = true;
            _owner._speech.Speak(
                // Translators: Spoken while another tab of a YouTube channel is being loaded.
                Tr("Loading..."),
                // Translators: The short wording spoken while a channel tab is being loaded.
                Tr("Loading..."));
            var channelBase = _session.ChannelBase;
            var tabKey = ChannelTabs.Keys[tabIndex];
            var token = _session.Token;
            _ = Task.Run(() => _owner._backend.OpenChannelTab(channelBase, tabKey, token), token).ContinueWith(
                finished => _owner._dispatcher.Post(() => Switched(tabIndex, finished, replaced)),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private void Switched(
            int tabIndex,
            Task<Backend.ChannelTabResult> finished,
            Action<IReadOnlyList<YouTubeResult>?> replaced)
        {
            _loading = false;
            // The window closed, or another list replaced this session, while the tab was on its way. The
            // page that came back has no home now, so it is closed here rather than leaked, and the caller
            // is not told - its callback belongs to a dialog that has gone.
            if (_closed || _session.IsCancelled || !ReferenceEquals(_owner._session, _session))
            {
                if (finished.IsCompletedSuccessfully && finished.Result.Page is IAsyncDisposable page)
                    _ = page.DisposeAsync().AsTask();
                return;
            }
            if (!finished.IsCompletedSuccessfully || finished.Result.Failure is not ResolveFailure.None)
            {
                replaced(null);
                return;
            }
            var result = finished.Result;
            _session.CacheTab(tabIndex, result.Items, result.Page);
            replaced(result.Items);
            _owner.Prefetch(_session, 0, OpeningPrefetch);
        }

        private void Arrived(Task<IReadOnlyList<YouTubeResult>> finished, Action<IReadOnlyList<YouTubeResult>> appended)
        {
            _loading = false;
            if (_session.IsCancelled || !ReferenceEquals(_owner._session, _session))
                return;
            // A page that failed is not the end of the results, only the end of this attempt: leaving
            // _exhausted alone lets the user try again by arrowing off the last row and back onto it.
            if (!finished.IsCompletedSuccessfully)
                return;
            var page = finished.Result;
            if (page.Count == 0)
            {
                _session.Exhausted = true;
                if (!_closed)
                    appended([]);
                return;
            }
            // Kept on the session before anything else, and kept even when the window has gone. Taking a
            // page consumes the search's continuation, so a page dropped here could never be asked for
            // again; this way closing the list while one is in flight only defers it to the next opening.
            var start = _session.Items.Count;
            _session.Append(page);
            if (!_closed)
                appended(page);
            _owner.Prefetch(_session, start, OpeningPrefetch);
        }
    }
}
