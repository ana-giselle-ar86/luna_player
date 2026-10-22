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
            && _fader.Level > 0
            && ReadBoolean("seekable") == true
            && ReadBoolean("eof-reached") != true
            && Elapsed is double pausedAt
            && BeginResumeSeek(pausedAt))
            return;
        ResumePlayback();
    }

    private bool BeginResumeSeek(double position)
    {
        // A pause outside the faded transport (for example, a selection boundary) can retain audible
        // samples in the device. Refresh those while paused. A completed fade-out needs no refresh.
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
        CancelSeekFade();
        _pauseRequested = false;
        if (_fadePauseResume && _fadeIntervalMilliseconds > 0)
        {
            var wasPaused = ReadBoolean("pause") == true;
            if (wasPaused)
                _fader.Set(0);
            // Arm the native envelope before unpausing. It advances only when the AO consumes audio,
            // so device startup cannot spend the fade interval or leak a full-volume first frame.
            _fader.FadeTo(1, _fadeIntervalMilliseconds, () =>
            {
                if (_pauseRequested == false)
                    _pauseRequested = null;
            });
            if (!TrySetProperty("pause", false))
            {
                _pauseRequested = null;
                _fader.Set(1);
                return;
            }
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
        CancelSeekFade();
        CancelEndFade(restoreVolume: false);
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
        if (_fadeSeeking && _loaded && !IsPaused
            && (_seekFadeTarget ?? Elapsed) is double position)
            FadeAroundSeek(Precision.Normalize(Math.Max(0, position + delta)));
        else
        {
            CancelEndFade();
            SeekRelativeCore(delta);
        }
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
        if (_fadeSeeking && _loaded && !IsPaused)
            FadeAroundSeek(target);
        else
        {
            CancelEndFade();
            SeekAbsoluteCore(target);
        }
    }

    private bool SeekRelativeCore(double delta)
    {
        if (_endBehaviorRestorePending
            && Elapsed is double position
            && BeginSelectionSeek(position + delta, restoreEndBehavior: true, play: !IsPaused))
            return true;
        return TryDo(mpv => mpv.Command("seek", delta, "relative"));
    }

    private bool SeekAbsoluteCore(double target, bool faded = false)
    {
        if (_endBehaviorRestorePending
            && BeginSelectionSeek(target, restoreEndBehavior: true, play: !IsPaused, preserveSeekFade: faded))
            return true;
        return TryDo(mpv => mpv.Command("seek", target, faded ? "absolute+exact" : "absolute"));
    }

    private void FadeAroundSeek(double target)
    {
        if (Duration is double duration && duration > 0)
            target = Math.Min(target, duration);
        CancelEndFade(restoreVolume: false);
        if (_seekFadeTarget.HasValue)
        {
            // While fading out, only replace the destination. Once muted, newer requests can seek
            // immediately; relative requests accumulate against this target rather than stale PTS.
            _seekFadeTarget = target;
            if (_seekFadeIssued)
                IssueFadedSeek();
            return;
        }

        _seekFadeActive = true;
        _seekFadeTarget = target;
        _seekFadeIssued = false;
        _pauseRequested = null;
        // A short sample-level de-click ramp silences the outgoing position before the jump. The
        // configured seek interval belongs to the fade-in, and rapid keys never queue extra ramps.
        _fader.FadeTo(0, Math.Min(5, _seekFadeIntervalMilliseconds), IssueFadedSeek);
    }

    private void IssueFadedSeek()
    {
        if (_disposed || _seekFadeTarget is not double target)
            return;
        _seekFadeIssued = true;
        if (!SeekAbsoluteCore(target, faded: true))
        {
            CancelSeekFade();
            _fader.Set(1);
        }
    }

    private void CancelSeekFade()
    {
        if (_seekFadeActive)
            _fader.Set(_fader.Level);
        _seekFadeActive = false;
        _seekFadeTarget = null;
        _seekFadeIssued = false;
    }

    private bool HasReachedFadedSeek(double target)
    {
        if (ReadBoolean("seeking") == true || Elapsed is not double position)
            return false;
        if (Math.Abs(position - target) <= 0.5 || ReadBoolean("eof-reached") == true)
            return true;
        // An exact seek to an A-B loop's end can restart at its start instead. Do not leave that
        // legitimate restart muted while waiting for a timestamp the loop will never reach.
        return ReadDouble("ab-loop-a") is double start && ReadDouble("ab-loop-b") is double end
            && end > start && target >= end && position >= start && position <= start + 0.5;
    }

}
