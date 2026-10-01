using LunaPlayer.Accessibility;
using LunaPlayer.Actions;
using LunaPlayer.Configuration;
using LunaPlayer.Playback;
using LunaPlayer.UI;

namespace LunaPlayer.Application.ActionHandlers;

/// <summary>Reads embedded subtitles aloud. The Subtitles menu lists the file's tracks with Off at the top;
/// choosing one has mpv decode it, and each line it puts on screen is spoken as it comes. A quick key turns
/// reading on and off without opening the menu.</summary>
/// <remarks>
/// Like the equalizer, the menu's track choices do not travel through <see cref="ActionRouter"/>: which
/// subtitle tracks exist changes with the file, so there is no fixed action for one. Only the on/off toggle
/// is a bound action. mpv's <c>sid</c> is the single source of truth for what is being read; the menu's
/// ticked item and the toggle both read and drive it.
/// </remarks>
internal sealed class SubtitleActions : IDisposable
{
    private readonly IMainView _view;
    private readonly MediaPlayer _player;
    private readonly ISpeechOutput _speech;
    private readonly PlayerSettings _settings;
    private readonly IApplicationDispatcher _dispatcher;
    // The track last read, so the toggle can bring it back after an off. Reset to the first track when the
    // remembered id is not among the current file's tracks, since ids do not carry across files.
    private int? _lastTrackId;

    internal SubtitleActions(
        ActionRouter router,
        IMainView view,
        MediaPlayer player,
        ISpeechOutput speech,
        PlayerSettings settings,
        IApplicationDispatcher dispatcher)
    {
        _view = view;
        _player = player;
        _speech = speech;
        _settings = settings;
        _dispatcher = dispatcher;
        router.Register(ActionId.ToggleSubtitles, Toggle);
        _view.SubtitleTrackRequested += Choose;
        // Both fire on mpv's event thread, so each hop back to the UI thread goes through the dispatcher.
        _player.SubtitleTracksChanged += OnTracksChanged;
        _player.SubtitleTextChanged += OnSubtitleText;
    }

    // The user picked a row in the Subtitles menu: a track id, or null for Off.
    private void Choose(int? id)
    {
        if (id is null)
        {
            TurnOff(announce: true);
            return;
        }
        var tracks = _player.GetSubtitleTracks();
        var index = tracks.ToList().FindIndex(track => track.Id == id.Value);
        if (index >= 0)
            Enable(tracks[index]);
    }

    // The quick on/off key. With no subtitles it says so; otherwise it flips between the active track and Off,
    // bringing back the last track that was read when turning back on.
    private void Toggle()
    {
        var tracks = _player.GetSubtitleTracks();
        if (tracks.Count == 0)
        {
            _speech.Speak(
                // Translators: Spoken when the subtitle key is pressed but the file carries no subtitles.
                Tr("No subtitles in this file."),
                // Translators: The short wording spoken when the file has no subtitles.
                Tr("No subtitles."));
            return;
        }
        if (tracks.Any(track => track.Selected))
        {
            TurnOff(announce: true);
            return;
        }
        var index = _lastTrackId is int last ? tracks.ToList().FindIndex(track => track.Id == last) : -1;
        Enable(tracks[index < 0 ? 0 : index]);
    }

    private void Enable(SubtitleTrack track)
    {
        if (!_player.SetSubtitleTrack(track.Id))
        {
            _speech.Speak(
                // Translators: Spoken when the player could not start reading the subtitle track the user picked.
                Tr("Could not turn on subtitles."),
                // Translators: The short wording spoken when a subtitle track could not be turned on.
                Tr("Subtitles failed."));
            return;
        }
        _lastTrackId = track.Id;
        _view.SetSubtitleSelection(track.Id);
        _speech.Speak(
            // Translators: Spoken when subtitle reading starts. {name} is the subtitle's name, such as "English".
            TrFormat("Reading subtitles: {name}", Describe(track)),
            Describe(track));
    }

    private void TurnOff(bool announce)
    {
        _player.DisableSubtitles();
        _view.SetSubtitleSelection(null);
        if (announce)
            _speech.Speak(
                // Translators: Spoken when subtitle reading is turned off.
                Tr("Subtitles off."),
                // Translators: The short wording spoken when subtitle reading is turned off.
                Tr("Subtitles off"));
    }

    private void OnTracksChanged() => _dispatcher.Post(RebuildMenu);

    private void RebuildMenu()
    {
        var tracks = _player.GetSubtitleTracks();
        var entries = tracks.Select(track => new SubtitleMenuEntry(track.Id, Describe(track))).ToArray();
        int? activeId = null;
        foreach (var track in tracks)
            if (track.Selected) { activeId = track.Id; break; }
        _view.RebuildSubtitleMenu(entries, activeId);
    }

    // Each subtitle line arrives on mpv's thread; speak it on the UI thread. interrupt follows the setting:
    // on, a new line cuts off the last so speech keeps pace; off, each line is read in full.
    private void OnSubtitleText(string text)
        => _dispatcher.Post(() => _speech.SpeakText(text, _settings.General.SubtitleInterrupt));

    // The track's own title, or a stand-in, then its language - the same shape as an audio track's label.
    private static string Describe(SubtitleTrack track)
    {
        // Translators: Stand-in name for a subtitle track that has no title of its own.
        var parts = new List<string>(2) { track.Title ?? Tr("Subtitle") };
        if (Localization.LanguageName(track.Language) is { Length: > 0 } language)
            parts.Add(language);
        return string.Join(", ", parts);
    }

    public void Dispose()
    {
        _view.SubtitleTrackRequested -= Choose;
        _player.SubtitleTracksChanged -= OnTracksChanged;
        _player.SubtitleTextChanged -= OnSubtitleText;
    }
}
