using LunaPlayer.Configuration;

namespace LunaPlayer.Playback;

internal sealed partial class Engine
{
    public bool TogglePause()
    {
        if (IsPaused)
        {
            Play();
            return true;
        }
        Pause();
        return false;
    }

    public void Play()
    {
        // Clearing an mpv end boundary does not release its EOF latch. When playback is explicitly resumed
        // from the free slot, seek to the exact current timestamp to re-arm decoding, then play after mpv
        // confirms that zero-distance seek. Entering the free slot itself never changes time or playback.
        if (_selectionSeekPending)
            return;
        if (_endBehaviorRestorePending
            && (IsPaused || _releasedSelectionWasCompleted)
            && (Elapsed ?? (_releasedSelectionEndPending ? _releasedSelectionEnd : null)) is double position
            && BeginSelectionSeek(position, restoreEndBehavior: true, play: true))
            return;
        if (!IsPaused || _resumeSeekPosition.HasValue)
            return;
        if (_fadePauseResume && _fadeIntervalMilliseconds > 0
            && ReadBoolean("pause") == true
            && ReadBoolean("seekable") == true
            && ReadBoolean("eof-reached") != true
            && Elapsed is double pausedAt
            && BeginResumeSeek(pausedAt))
            return;
        ResumePlayback();
    }

    private bool BeginResumeSeek(double position)
    {
        // WASAPI retains samples already mixed at the old volume when it pauses. Setting volume
        // to zero cannot change those samples. Refresh at the exact paused timestamp while still
        // paused, then unpause only after playback-restart confirms the old output was discarded.
        _pauseRequested = false;
        _fader.Set(0);
        _resumeSeekPosition = position;
        if (TryDo(mpv => mpv.Command("seek", position, "absolute+exact")))
            return true;
        _resumeSeekPosition = null;
        return false;
    }

    private void ResumePlayback()
    {
        _pauseRequested = false;
        if (_fadePauseResume && _fadeIntervalMilliseconds > 0)
        {
            var wasPaused = ReadBoolean("pause") == true;
            Func<bool>? startWhen = null;
            if (wasPaused && ReadDouble("audio-pts") is double pausedAudioPosition)
            {
                // audio-pts accounts for the output delay. Do not consume the fade interval while
                // the device is starting; an unavailable audio clock falls back to immediate fading.
                startWhen = () => ReadDouble("audio-pts") is not double position || position > pausedAudioPosition;
            }
            // An unfinished fade-out reverses from its current level without seeking or hard muting.
            _fader.Set(wasPaused ? 0 : _fader.Level);
            if (!TrySetProperty("pause", false))
            {
                _pauseRequested = null;
                _fader.Set(1);
                return;
            }
            _fader.FadeTo(1, _fadeIntervalMilliseconds, () =>
            {
                if (_pauseRequested == false)
                    _pauseRequested = null;
            }, startWhen);
        }
        else
        {
            _fader.Set(1);
            SetPropertySafely("pause", false);
            _pauseRequested = null;
        }
    }

    public void Pause()
    {
        if (_resumeSeekPosition.HasValue)
        {
            // A second pause cancels the pending resume, not the refresh already in progress.
            _resumeSeekPosition = null;
            SetPropertySafely("pause", true);
            _pauseRequested = null;
            return;
        }
        if (IsPaused)
            return;
        CancelEndFade();
        if (!_fadePauseResume || _fadeIntervalMilliseconds <= 0)
        {
            SetPropertySafely("pause", true);
            _fader.Set(1);
            _pauseRequested = null;
            return;
        }
        _pauseRequested = true;
        _fader.FadeTo(0, _fadeIntervalMilliseconds, () =>
        {
            if (_disposed || _pauseRequested != true)
                return;
            SetPropertySafely("pause", true);
            // Restoring full volume here leaves the next resume with an unnecessary hard mute.
            _pauseRequested = null;
        });
    }

    public bool IsPaused => _pauseRequested ?? ReadBoolean("pause") ?? false;

    public bool SelectionRangeCompleted
        => _selectionRangeActive
            && !_selectionRangeRepeats
            && (_selectionRangeCompleted || ReadBoolean("eof-reached") == true);

    public double? Duration => Precision.Normalize(ReadDouble("duration"));

    public double? Elapsed => Precision.Normalize(ReadDouble("time-pos"));

    public double? Remaining => Precision.Normalize(ReadDouble("time-remaining"));

    public void SeekRelative(double seconds)
    {
        var delta = Precision.Normalize(seconds);
        if (_resumeSeekPosition is double resumeAt)
        {
            SeekAbsolute(resumeAt + delta);
            return;
        }
        FadeAroundSeek(() => SeekRelativeCore(delta));
    }

    public void SeekAbsolute(double seconds)
    {
        var target = Precision.Normalize(Math.Max(0, seconds));
        if (_resumeSeekPosition.HasValue)
        {
            // Replace the pending target so rapid seeks do not resume from an older request.
            if (!BeginResumeSeek(target))
                ResumePlayback();
            return;
        }
        FadeAroundSeek(() => SeekAbsoluteCore(target));
    }

    private bool SeekRelativeCore(double delta)
    {
        if (_endBehaviorRestorePending
            && Elapsed is double position
            && BeginSelectionSeek(position + delta, restoreEndBehavior: true, play: !IsPaused))
            return true;
        return TryDo(mpv => mpv.Command("seek", delta, "relative"));
    }

    private bool SeekAbsoluteCore(double target)
    {
        if (_endBehaviorRestorePending
            && BeginSelectionSeek(target, restoreEndBehavior: true, play: !IsPaused))
            return true;
        return TryDo(mpv => mpv.Command("seek", target, "absolute"));
    }

    private void FadeAroundSeek(Func<bool> seek)
    {
        CancelEndFade();
        if (!_fadeSeeking || !_loaded)
        {
            seek();
            return;
        }

        // Seeking must remain responsive when a key is pressed repeatedly. Silence the current sample,
        // move immediately, then fade the new position in; another seek simply replaces this fade.
        _seekFadeActive = true;
        _fader.Set(0);
        if (!seek())
        {
            _seekFadeActive = false;
            _fader.Set(1);
            return;
        }
        _fader.FadeTo(1, _seekFadeIntervalMilliseconds, () => _seekFadeActive = false);
    }

}
