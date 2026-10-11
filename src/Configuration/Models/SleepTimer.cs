using System.Text.Json.Serialization;

namespace LunaPlayer.Configuration.Models;

internal enum SleepTimerMode { Duration, EndOfTrack }
internal enum SleepTimerAction { Pause, Stop }

/// <summary>The sleep-timer choices remembered between sessions, so the window opens on what was set last.
/// </summary>
///
/// <remarks>
/// These are the last-used preferences, not a timer that survives a restart: closing the player always
/// ends any countdown. The window is prefilled from here, and its choices are written back here when the
/// user sets a timer.
/// </remarks>
internal sealed class SleepTimerSettings
{
    internal const int MinimumDuration = 1;
    internal const int MaximumDuration = 24 * 60;
    internal const int MinimumFadeSeconds = 1;
    internal const int MaximumFadeSeconds = 120;

    [JsonConverter(typeof(JsonStringEnumConverter<SleepTimerMode>))]
    public SleepTimerMode Mode { get; set; } = SleepTimerMode.Duration;

    [JsonConverter(typeof(JsonStringEnumConverter<SleepTimerAction>))]
    public SleepTimerAction Action { get; set; } = SleepTimerAction.Pause;

    /// <summary>How long a duration timer runs, in minutes.</summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>Whether the volume is faded down over the last seconds before the timer fires.</summary>
    public bool Fade { get; set; } = true;

    /// <summary>How many seconds the fade lasts.</summary>
    public int FadeSeconds { get; set; } = 20;

    internal SleepTimerSettings Copy() => (SleepTimerSettings)MemberwiseClone();

    internal void Apply(SleepTimerSettings source)
    {
        Mode = source.Mode;
        Action = source.Action;
        DurationMinutes = source.DurationMinutes;
        Fade = source.Fade;
        FadeSeconds = source.FadeSeconds;
    }
}
