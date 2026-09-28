namespace LunaPlayer.Actions;

/// <summary>The commands for the sleep timer: setting one, cancelling it, and hearing the time left.</summary>
///
/// <remarks>
/// Kept apart from the other definitions like the IPTV and YouTube commands are (see
/// <see cref="IptvActionDefinitions"/>): the sleep timer is a self-contained feature. Setting the timer
/// carries a default shortcut because it is the door in; cancelling and asking for the remaining time are
/// reached from the same submenu and carry no default key, so nothing is spent on rarely pressed commands.
/// </remarks>
internal static class SleepTimerActionDefinitions
{
    internal static IReadOnlyList<ActionDefinition> All { get; } =
    [
        // Translators: Name of the command that opens the window where a sleep timer is set.
        new(ActionId.OpenSleepTimer, Tr("Set sleep timer..."),
            new("s", ShortcutModifiers.Control | ShortcutModifiers.Shift)),
        // Translators: Name of the command that cancels a sleep timer that is counting down.
        new(ActionId.CancelSleepTimer, Tr("Cancel sleep timer")),
        // Translators: Name of the command that speaks how long is left before the sleep timer fires.
        new(ActionId.AnnounceSleepTimerRemaining, Tr("Sleep timer remaining")),
    ];
}
