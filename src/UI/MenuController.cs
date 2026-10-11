using System.Globalization;
using LunaPlayer.Actions;
using LunaPlayer.Configuration;
using LunaPlayer.Equalizer;
using LunaPlayer.Recording;
using WxSharp;

namespace LunaPlayer.UI;

/// <summary>Owns the menu bar and everything hanging off it: the command-id maps the window routes through,
/// the groups of items that are enabled and disabled together, and the dynamic equalizer, subtitle and
/// recents submenus that are rebuilt as the player runs. The main window holds one of these and forwards its
/// <see cref="IMenuState"/> calls here, so the menu bookkeeping lives in one place rather than spread across
/// the window.</summary>
internal sealed class MenuController
{
    private readonly Dictionary<ActionId, int> _commandIds = [];
    private readonly Dictionary<int, ActionId> _commands = [];
    private readonly List<MenuItem> _playbackItems = [];
    private readonly List<MenuItem> _mediaFileItems = [];
    private readonly List<MenuItem> _localFileItems = [];
    private readonly List<MenuItem> _markedItems = [];
    private readonly List<MenuItem> _videoItems = [];
    private readonly List<MenuItem> _audioTrackItems = [];
    private readonly List<MenuItem> _localEditItems = [];
    private readonly List<MenuItem> _bookmarkItems = [];
    private readonly Dictionary<int, string> _equalizerCommands = [];
    private readonly Dictionary<string, MenuItem> _equalizerItems = new(StringComparer.Ordinal);
    private readonly Menu _equalizerMenu;
    private readonly Dictionary<int, string> _subtitleCommands = [];
    private readonly Dictionary<string, MenuItem> _subtitleItems = new(StringComparer.Ordinal);
    private readonly Menu _subtitleMenu;
    private readonly MenuItem _subtitleMenuItem;
    private readonly MenuItem _subtitleRememberItem;
    private readonly Dictionary<int, RecentCommand> _recentCommands = [];
    private readonly Menu _recentFilesMenu;
    private readonly Menu _recentFoldersMenu;
    private readonly Menu _recentPlaylistsMenu;
    private readonly MenuBar _menuBar;
    private readonly int _bookmarksMenuIndex;
    private readonly int _markedMenuIndex;
    private readonly int _videoMenuIndex;
    private readonly MenuItem _markCurrentItem;
    private readonly MenuItem _markAllItem;
    private readonly MenuItem _shuffleItem;
    private readonly MenuItem _repeatFileItem;
    private readonly MenuItem _silenceRemovalItem;
    private readonly MenuItem _fullScreenItem;
    private readonly MenuItem _startRecordingItem;
    private readonly MenuItem _pauseRecordingItem;
    private readonly MenuItem _stopRecordingItem;
    private ShortcutManager _shortcuts;
    internal MenuController(
        Frame frame,
        IReadOnlyList<ActionDefinition> actions,
        ShortcutManager shortcuts,
        IReadOnlyList<PresetEntry> presets)
    {
        _shortcuts = shortcuts;
        BuildCommandIds(actions);
        var menu = MainMenuBuilder.Build(frame, _commandIds, shortcuts, presets);
        _menuBar = menu.MenuBar;
        _bookmarksMenuIndex = menu.BookmarksMenuIndex;
        _markedMenuIndex = menu.MarkedMenuIndex;
        _videoMenuIndex = menu.VideoMenuIndex;
        _playbackItems.AddRange(menu.PlaybackItems);
        _mediaFileItems.AddRange(menu.MediaFileItems);
        _localFileItems.AddRange(menu.LocalFileItems);
        _markedItems.AddRange(menu.MarkedItems);
        _videoItems.AddRange(menu.VideoItems);
        _audioTrackItems.AddRange(menu.AudioTrackItems);
        _localEditItems.AddRange(menu.LocalEditItems);
        _bookmarkItems.AddRange(menu.BookmarkItems);
        foreach (var command in menu.EqualizerCommands)
            _equalizerCommands[command.Key] = command.Value;
        foreach (var item in menu.EqualizerItems)
            _equalizerItems[item.Key] = item.Value;
        _equalizerMenu = menu.EqualizerMenu;
        foreach (var command in menu.SubtitleCommands)
            _subtitleCommands[command.Key] = command.Value;
        foreach (var item in menu.SubtitleItems)
            _subtitleItems[item.Key] = item.Value;
        _subtitleMenu = menu.SubtitleMenu;
        _subtitleMenuItem = menu.SubtitleMenuItem;
        _subtitleRememberItem = menu.SubtitleRememberItem;
        _recentFilesMenu = menu.RecentFilesMenu;
        _recentFoldersMenu = menu.RecentFoldersMenu;
        _recentPlaylistsMenu = menu.RecentPlaylistsMenu;
        _markCurrentItem = menu.MarkCurrentItem;
        _markAllItem = menu.MarkAllItem;
        _shuffleItem = menu.ShuffleItem;
        _repeatFileItem = menu.RepeatFileItem;
        _silenceRemovalItem = menu.SilenceRemovalItem;
        _fullScreenItem = menu.FullScreenItem;
        _startRecordingItem = menu.StartRecordingItem;
        _pauseRecordingItem = menu.PauseRecordingItem;
        _stopRecordingItem = menu.StopRecordingItem;
    }

    /// <summary>The menu-command id reserved for each action, which the window turns an accelerator key or a
    /// menu click back into an action through.</summary>
    internal IReadOnlyDictionary<ActionId, int> CommandIds => _commandIds;

    /// <summary>Resolves a menu-command id to the fixed action it stands for, if any.</summary>
    internal bool TryGetAction(int commandId, out ActionId action) => _commands.TryGetValue(commandId, out action);

    /// <summary>Resolves a menu-command id to the equalizer preset it selects: the preset's id, or null for
    /// the Off item. Returns false when the id is not an equalizer entry.</summary>
    internal bool TryGetEqualizerPreset(int commandId, out string? presetId)
    {
        if (_equalizerCommands.TryGetValue(commandId, out var key))
        {
            presetId = key.Length == 0 ? null : key;
            return true;
        }
        presetId = null;
        return false;
    }

    /// <summary>Resolves a menu-command id to the subtitle track it selects: the track id, or null for the
    /// Off item. Returns false when the id is not a subtitle entry.</summary>
    internal bool TryGetSubtitleTrack(int commandId, out int? trackId)
    {
        if (_subtitleCommands.TryGetValue(commandId, out var key))
        {
            trackId = key.Length == 0 ? null : int.Parse(key, CultureInfo.InvariantCulture);
            return true;
        }
        trackId = null;
        return false;
    }

    /// <summary>Resolves a menu-command id to the recents command it stands for, if any.</summary>
    internal bool TryGetRecent(int commandId, out RecentCommand recent) => _recentCommands.TryGetValue(commandId, out recent);
    internal void SetShuffleChecked(bool isChecked) => _shuffleItem.Checked = isChecked;

    internal void SetRepeatFileChecked(bool isChecked) => _repeatFileItem.Checked = isChecked;

    internal void SetSilenceRemovalChecked(bool isChecked) => _silenceRemovalItem.Checked = isChecked;

    /// <summary>Ticks or clears the full-screen menu item to match the window. The window owns going full
    /// screen; this only keeps the item in step.</summary>
    internal void SetFullScreenChecked(bool fullScreen) => _fullScreenItem.Checked = fullScreen;

    internal void SetFullScreenAvailable(bool available) => _fullScreenItem.Enabled = available;

    internal void SetEqualizerPreset(string? presetId)
    {
        // Callers only ever name a preset the menu was built from, or nothing at all. The lookup is a
        // guard against a name that is neither rather than a case that is expected to happen.
        var key = presetId ?? MainMenuBuilder.EqualizerOffKey;
        if (_equalizerItems.TryGetValue(key, out var item))
            item.Checked = true;
    }

    internal void RebuildEqualizerMenu(IReadOnlyList<PresetEntry> presets, string? selected)
    {
        // Emptied and filled again rather than replaced. The submenu is already attached to the menu bar,
        // and swapping it would leave the bar pointing at a menu nothing else knows about.
        while (_equalizerMenu.Count > 0)
            _equalizerMenu.Delete(_equalizerMenu[0]);
        _equalizerCommands.Clear();
        _equalizerItems.Clear();
        MainMenuBuilder.FillEqualizerMenu(
            _equalizerMenu, presets, _commandIds, _shortcuts, _equalizerCommands, _equalizerItems);
        SetEqualizerPreset(selected);
    }

    internal void RebuildSubtitleMenu(IReadOnlyList<SubtitleMenuEntry> entries, int? activeId)
    {
        // Only the dynamic radio region is rebuilt - the items above the first separator. The fixed commands
        // below it (load from file/URL, remember) keep their permanent action ids; deleting them would free
        // those ids, and the next rebuild re-adding the same id trips a wxWidgets auto-id assert.
        while (_subtitleMenu.Count > 0 && _subtitleMenu[0].Kind != MenuItemKind.Separator)
            _subtitleMenu.Delete(_subtitleMenu[0]);
        _subtitleCommands.Clear();
        _subtitleItems.Clear();
        MainMenuBuilder.FillSubtitleTracks(_subtitleMenu, entries, activeId, _subtitleCommands, _subtitleItems);
    }

    internal void SetSubtitleRemember(bool on) => _subtitleRememberItem.Checked = on;

    internal void SetSubtitleSelection(int? activeId)
    {
        var key = activeId?.ToString(CultureInfo.InvariantCulture) ?? MainMenuBuilder.SubtitleOffKey;
        if (_subtitleItems.TryGetValue(key, out var item))
            item.Checked = true;
    }

    internal void SetSubtitleControlsEnabled(bool enabled) => _subtitleMenuItem.Enabled = enabled;
    internal void RebuildRecentsMenu(
        IReadOnlyList<RecentMenuEntry> files,
        IReadOnlyList<RecentMenuEntry> folders,
        IReadOnlyList<RecentMenuEntry> playlists)
    {
        // One shared command map for all three submenus, cleared once and refilled with fresh ids.
        _recentCommands.Clear();
        FillRecents(_recentFilesMenu, RecentKind.File, files,
            // Translators: Shown in the Recent files submenu when no files have been opened yet.
            Tr("No recent files"),
            // Translators: Recent files submenu item that empties the list of recently opened files.
            Tr("Clear recent files"));
        FillRecents(_recentFoldersMenu, RecentKind.Folder, folders,
            // Translators: Shown in the Recent folders submenu when no folders have been opened yet.
            Tr("No recent folders"),
            // Translators: Recent folders submenu item that empties the list of recently opened folders.
            Tr("Clear recent folders"));
        FillRecents(_recentPlaylistsMenu, RecentKind.Playlist, playlists,
            // Translators: Shown in the Recent playlists submenu when no playlists have been opened yet.
            Tr("No recent playlists"),
            // Translators: Recent playlists submenu item that empties the list of recently opened playlists.
            Tr("Clear recent playlists"));
    }

    private void FillRecents(Menu menu, RecentKind kind, IReadOnlyList<RecentMenuEntry> entries,
        string emptyLabel, string clearLabel)
    {
        while (menu.Count > 0)
            menu.Delete(menu[0]);
        MainMenuBuilder.FillRecentsSubmenu(menu, kind, entries, emptyLabel, clearLabel, _recentCommands);
    }

    internal void SetMediaLoaded(bool loaded)
    {
        foreach (var item in _playbackItems)
            item.Enabled = loaded;
        foreach (var item in _mediaFileItems)
            item.Enabled = loaded;
    }

    internal void SetEditState(bool hasLocalFile, bool hasMedia)
    {
        foreach (var item in _localEditItems)
            item.Enabled = hasLocalFile;
        foreach (var item in _localFileItems)
            item.Enabled = hasLocalFile;
        _markCurrentItem.Enabled = hasMedia;
        _markAllItem.Enabled = hasMedia;
    }

    internal void SetBookmarkState(bool enabled)
    {
        foreach (var item in _bookmarkItems)
            item.Enabled = enabled;
        _menuBar.EnableTop(_bookmarksMenuIndex, enabled);
    }

    internal void SetMarkState(bool currentMarked, bool allMarked)
    {
        _markCurrentItem.Checked = currentMarked;
        _markAllItem.Checked = allMarked;
    }
    internal void SetRecordingState(RecordingState state)
    {
        var idle = state is RecordingState.Idle;
        _startRecordingItem.Enabled = idle;
        // Both of these need a recording to act on, and neither cares whether it is running or held.
        _pauseRecordingItem.Enabled = !idle;
        _stopRecordingItem.Enabled = !idle;
        // Said the way the button in the window says it, so the menu and the window never disagree about
        // what the key does. The shortcut is kept: it is the part after the tab, which the menu owns.
        var parts = _pauseRecordingItem.Label.Split('	');
        var label = state is RecordingState.Paused
            // Translators: Recording menu item that starts a paused recording again.
            ? Tr("Resume")
            // Translators: Recording menu item that holds a recording where it is.
            : Tr("Pause");
        _pauseRecordingItem.Label = parts.Length > 1 ? $"{label}	{parts[1]}" : label;
    }

    internal void SetVideoOptionsEnabled(bool enabled)
    {
        foreach (var item in _videoItems) item.Enabled = enabled;
        _menuBar.EnableTop(_videoMenuIndex, enabled);
    }

    internal void SetAudioTrackControlsEnabled(bool enabled)
    {
        foreach (var item in _audioTrackItems) item.Enabled = enabled;
    }

    internal void SetMarkedActionsEnabled(bool enabled)
    {
        foreach (var item in _markedItems) item.Enabled = enabled;
        _menuBar.EnableTop(_markedMenuIndex, enabled);
    }

    /// <summary>Takes a new set of shortcuts and relabels every menu item to show the key now bound to it,
    /// keeping the label text and swapping only the part after the tab. The window rebuilds its accelerator
    /// table from the same set separately.</summary>
    internal void ApplyShortcuts(ShortcutManager shortcuts)
    {
        _shortcuts = shortcuts;
        foreach (var pair in _commandIds)
        {
            var item = _menuBar.FindItem(pair.Value);
            if (item is null || item.Kind == MenuItemKind.Separator) continue;
            var baseLabel = item.Label.Split('\t')[0];
            item.Label = shortcuts.Get(pair.Key) is Shortcut shortcut
                ? $"{baseLabel}\t{shortcut.ToDisplayString()}"
                : baseLabel;
        }
    }

    private void BuildCommandIds(IEnumerable<ActionDefinition> actions)
    {
        foreach (var action in actions)
        {
            if (action.Id == ActionId.OpenFile)
                AddCommand(action.Id, StandardId.Open);
            else if (action.Id == ActionId.Exit)
                AddCommand(action.Id, StandardId.Exit);
            else
                AddCommand(action.Id, IdManager.NewId());
        }
    }

    /// <remarks>
    /// An id from <see cref="IdManager.NewId"/> is never released. Putting it on a menu hands the
    /// reservation to a <c>wxWindowIDRef</c>, and destroying the menu bar with the frame returns it; a
    /// release afterwards would free it twice, which wxWidgets asserts on in a debug build.
    /// </remarks>
    private void AddCommand(ActionId action, int id)
    {
        _commandIds[action] = id;
        _commands[id] = action;
    }
}

