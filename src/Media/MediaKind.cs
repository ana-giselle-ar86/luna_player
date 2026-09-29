namespace LunaPlayer.Media;

/// <summary>The three kinds of thing the player opens. See <see cref="MediaLibrary.KindOf"/> for the
/// extension-based guess and <see cref="Playback.IPlaybackEngine.HasVideo"/> for mpv's runtime answer.</summary>
internal enum MediaKind
{
    Audio,
    Video,
    Playlist,
}
