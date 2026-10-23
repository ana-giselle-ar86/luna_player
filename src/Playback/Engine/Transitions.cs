using LunaPlayer.Configuration;
using LunaPlayer.Configuration.Models;
using MpvNet;

namespace LunaPlayer.Playback;

internal sealed partial class Engine
{
    private bool BeginSelectionSeek(double seconds, bool restoreEndBehavior, bool play)
    {
        var target = Precision.Normalize(Math.Max(0, seconds));
        _selectionSeekPending = true;
        _restoreEndBehaviorAfterSelectionSeek = restoreEndBehavior;
        _playAfterSelectionSeek = play;
        _selectionSeekTarget = target;
        if (TryDo(mpv => mpv.Command("seek", target, "absolute+exact")))
            return true;
        CancelSelectionSeek();
        return false;
    }

    private void HandlePlaybackRestart(MpvEvent _)
    {
        if (_disposed)
            return;
        if (_awaitingLoadRestart && Paths.AreSame(ReadString("path"), _loadingPath))
        {
            _awaitingLoadRestart = false;
            _loadingPath = null;
            _loadingPaused = null;
            if (_queuedNext)
                _queuedSourceEntryId = PositiveEntryId();
        }
        if (_selectionSeekPending)
        {
            // Playback-restart events from an earlier seek may already be waiting in mpv's event queue.
            // Only finish this transition once the seek we issued is no longer running and its target is current.
            if (ReadBoolean("seeking") == true
                || Elapsed is double elapsed && Math.Abs(elapsed - _selectionSeekTarget) > 0.5)
                return;
            var restoreEndBehavior = _restoreEndBehaviorAfterSelectionSeek;
            var play = _playAfterSelectionSeek;
            CancelSelectionSeek();
            if (restoreEndBehavior || _endBehaviorRestorePending)
            {
                _endBehaviorRestorePending = false;
                ApplyEndBehavior();
            }
            if (play)
                ResumePlayback();
            SelectionSeekCompleted?.Invoke();
        }
        if (_fadeInOnRestart)
        {
            _fadeInOnRestart = false;
            _endFadeActive = false;
            // Loading already muted the new entry. Do not replace a transport request made while
            // it was opening; mpv owns that envelope until the pause, resume or seek is settled.
            if (!IsPaused && ReadBoolean("audio-fade/transport-active") != true)
                FadeVolume(1, _fadeIntervalMilliseconds);
        }
        if (_gaplessAdvancePending)
        {
            _gaplessAdvancePending = false;
            Ended?.Invoke(EndReason.GaplessAdvance);
        }
    }

    private void HandlePositionChange(string _, object? value)
    {
        UpdateEndFade(value);
        if (_disposed
            || !_releasedSelectionEndPending
            || _releasedSelectionPassed
            || !TryConvertDouble(value, out var position)
            || position <= _releasedSelectionEnd + 0.001)
            return;
        var entryId = ReadInt64("playlist-entry-id");
        if (_releasedSelectionEntryId.HasValue
            && entryId.HasValue
            && _releasedSelectionEntryId.Value != entryId.Value)
            return;
        // The old boundary was removed before it fired and playback has demonstrably crossed it. End
        // behaviour is safe to restore now; the pending identity remains until the eventual real EOF so
        // a delayed event can still be classified from its timestamp instead of advancing by mistake.
        _releasedSelectionPassed = true;
        _releasedSelectionWasPlaying = false;
        _releasedSelectionWasCompleted = false;
        if (_endBehaviorRestorePending)
        {
            _endBehaviorRestorePending = false;
            ApplyEndBehavior();
        }
    }

    private void UpdateEndFade(object? value)
    {
        if (_disposed || !ShouldFadeTransitions || _selectionRangeActive || _repeatFile
            || _endBehavior != EndBehavior.Advance || _queuedNext || IsPaused
            || ReadBoolean("audio-fade/transport-active") == true
            || (!_endFadeActive && ReadBoolean("audio-fade/active") == true)
            || !TryConvertDouble(value, out var position) || Duration is not double duration)
            return;
        var remaining = duration - position;
        var interval = _fadeIntervalMilliseconds / 1000.0;
        if (remaining > interval)
        {
            if (_endFadeActive)
            {
                _endFadeActive = false;
                SetFade(1);
            }
            return;
        }
        if (_endFadeActive)
            return;
        _endFadeActive = true;
        // Once armed, the output mixer owns every sample of the ramp. Position notifications must
        // not replace it with block-sized gain changes. Account for playback speed at the boundary.
        var speed = Math.Max(0.01, ReadDouble("speed") ?? 1);
        var milliseconds = (int)Math.Clamp(remaining / speed * 1000, 0, 60000);
        FadeVolume(0, milliseconds);
    }

    private void CancelSelectionSeek()
    {
        _selectionSeekPending = false;
        _restoreEndBehaviorAfterSelectionSeek = false;
        _playAfterSelectionSeek = false;
        _selectionSeekTarget = 0;
    }

}
