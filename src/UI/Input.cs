using System.Runtime.InteropServices;
using LunaPlayer.Actions;
using WxSharp;

namespace LunaPlayer.UI;

/// <summary>Everything that turns a key into a request on the main window: the local accelerator table, the
/// system-wide shortcuts, the menu-command clicks, and the bare Escape key. It resolves each through the
/// <see cref="MenuController"/>'s command maps and raises the events the window exposes, so the window itself
/// holds no keyboard plumbing. Also reads the live modifier state used to defer an Alt+F4 close.</summary>
internal sealed partial class WindowInput : IDisposable
{
    private readonly Frame _frame;
    private readonly MenuController _menus;
    private readonly GlobalShortcuts _globalShortcuts;

    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyAlt = 0x12;
    private const int VirtualKeyLeftWindows = 0x5B;
    private const int VirtualKeyRightWindows = 0x5C;
    private const int VirtualKeyF4 = 0x73;

    internal WindowInput(Frame frame, MenuController menus, ShortcutManager shortcuts)
    {
        _frame = frame;
        _menus = menus;
        _globalShortcuts = new GlobalShortcuts();
        _globalShortcuts.Pressed += Request;
        BuildAccelerators(shortcuts);
        _frame.MenuCommand += OnMenuCommand;
        _frame.Bind(WxEvents.CharHook, OnCharHook);
    }

    internal event Action<ActionId>? ActionRequested;
    internal event Action<string?>? EqualizerPresetRequested;
    internal event Action<int?>? SubtitleTrackRequested;
    internal event Action<RecentCommand>? RecentRequested;

    /// <summary>Asked when Escape is pressed with no modifier. Returning true means it was dealt with and
    /// the key goes no further.</summary>
    internal event Func<bool>? EscapePressed;

    /// <summary>The system-wide shortcut watcher, shared with the dialogs so a dialog can pause and resume
    /// it. Owned here and torn down with this object.</summary>
    internal GlobalShortcuts GlobalShortcuts => _globalShortcuts;

    /// <summary>Raises an action request from a source outside the keyboard - the window's transport buttons
    /// go through here so every action travels one path.</summary>
    internal void Request(ActionId action) => ActionRequested?.Invoke(action);

    internal bool ApplyGlobalShortcuts(ShortcutManager shortcuts)
        => _globalShortcuts.Apply(shortcuts.GetBindings());

    /// <summary>Builds the local accelerator table again from a new set of shortcuts.</summary>
    internal void ApplyAccelerators(ShortcutManager shortcuts) => BuildAccelerators(shortcuts);

    /// <summary>Stops the system-wide shortcuts without tearing the rest down, for the close sequence: no
    /// action should arrive once shutdown has begun. Disposing is idempotent, so the final
    /// <see cref="Dispose"/> may still run.</summary>
    internal void StopGlobalShortcuts() => _globalShortcuts.Dispose();

    public void Dispose() => _globalShortcuts.Dispose();
    private void BuildAccelerators(ShortcutManager shortcuts)
    {
        var accelerators = new List<AcceleratorEntry>();
        foreach (var binding in shortcuts.GetBindings())
        {
            // An accelerator table cannot express the Windows key, so a binding using it is left to the
            // global hot key registration instead of failing the whole table.
            if ((binding.Shortcut.Modifiers & ShortcutModifiers.Win) != 0) continue;
            var commandId = _menus.CommandIds[binding.Action];
            var display = binding.Shortcut.ToDisplayString();
            if (!AcceleratorEntry.TryParse(display, commandId, out var accelerator))
                throw new InvalidOperationException($"'{display}' is not a valid local shortcut.");
            accelerators.Add(accelerator);
        }
        _frame.SetAcceleratorTable([.. accelerators]);
    }

    private void OnMenuCommand(object? sender, CommandEventArgs args)
    {
        if (_menus.TryGetAction(args.Id, out var action))
        {
            Request(action);
            return;
        }
        // Guarded here rather than further along. Menu commands bound to an action are guarded where they
        // are dispatched, but these are not, and it is still wxWidgets calling in: an exception let past
        // would unwind into C++ frames.
        if (_menus.TryGetEqualizerPreset(args.Id, out var presetId))
        {
            LunaPlayer.Application.CrashReport.Guard(() => EqualizerPresetRequested?.Invoke(presetId));
            return;
        }
        if (_menus.TryGetSubtitleTrack(args.Id, out var trackId))
        {
            LunaPlayer.Application.CrashReport.Guard(() => SubtitleTrackRequested?.Invoke(trackId));
            return;
        }
        if (_menus.TryGetRecent(args.Id, out var recent))
            LunaPlayer.Application.CrashReport.Guard(() => RecentRequested?.Invoke(recent));
    }

    /// <remarks>
    /// Only a bare Escape, and only when something answers to it. Anything else is skipped so the window
    /// keeps behaving as it did: Escape is not a shortcut the user can bind, and swallowing it
    /// unconditionally would take away the key that closes things.
    /// </remarks>
    private void OnCharHook(object? sender, KeyEventArgs args)
    {
        if (args.Code == Key.Escape && args.Modifiers == KeyModifiers.None)
        {
            // Guarded here rather than at the far end: this runs inside a wxWidgets key event, and an
            // exception thrown past it would unwind into C++ frames and take the process with it.
            var handled = false;
            LunaPlayer.Application.CrashReport.Guard(() => handled = EscapePressed?.Invoke() == true);
            if (handled)
                return;
        }
        args.Skip();
    }

    /// <summary>Whether any of the keys that make up a window-closing chord (the modifiers, a Windows key, or
    /// F4) is physically down right now. Used to hold an Alt+F4 close until the chord is released.</summary>
    internal bool CloseChordIsDown()
        => IsKeyDown(VirtualKeyShift)
            || IsKeyDown(VirtualKeyControl)
            || IsKeyDown(VirtualKeyAlt)
            || IsKeyDown(VirtualKeyLeftWindows)
            || IsKeyDown(VirtualKeyRightWindows)
            || IsKeyDown(VirtualKeyF4);

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int virtualKey);
}
