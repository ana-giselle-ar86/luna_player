using LunaPlayer.Application;
using WxSharp;

namespace LunaPlayer.UI.YouTube;

/// <summary>The window that asks what to look for on YouTube, and offers to finish the query as it is
/// typed.</summary>
///
/// <remarks>
/// Above the plain query box and the filter
/// sits a list of suggestions that is hidden until there are any: as the user types, a debounce waits for
/// them to pause, then a background thread asks YouTube what it would finish the words with, and the answer
/// is shown and announced. The list is reached with Down from the box and left with Up from its first row;
/// Enter on a suggestion searches for it, Right drops it into the box to be refined, and Escape puts it
/// away. Nothing here blocks the UI thread - the fetch runs off it and marshals its answer back through the
/// dispatcher - and a fetch whose answer arrives after the box has moved on is dropped by the request
/// counter rather than shown out of turn.
/// </remarks>
internal sealed class SearchDialog : IDisposable
{
    /// <summary>How long after the last keystroke the suggestions are fetched.
    /// </summary>
    private const int SuggestionsDelayMs = 750;

    private readonly Dialog _dialog;
    private readonly TextCtrl _query;
    private readonly StaticText _suggestionsLabel;
    private readonly ListBox _suggestions;
    private readonly Choice _filter;
    private readonly Button _search;
    private readonly WxSharp.Timer _debounce;
    private readonly IApplicationDispatcher _dispatcher;
    private readonly YouTubeSearchPrompt _prompt;

    // Set true the moment the window is on its way out, so a suggestion fetch that finishes after that -
    // its Post already queued on the UI thread - returns without touching controls that may be gone.
    private bool _closing;
    // Bumped on every fetch; a returning answer whose id is no longer the latest is stale and dropped.
    private int _requestCounter;
    private YouTubeSearchRequest? _result;

    internal SearchDialog(Window parent, IApplicationDispatcher dispatcher, YouTubeSearchPrompt prompt)
    {
        _dispatcher = dispatcher;
        _prompt = prompt;
        _dialog = new Dialog(
            parent,
            // Translators: Title of the window that asks what to look for on YouTube.
            title: Tr("Search YouTube"),
            style: DialogStyle.Default | DialogStyle.ResizeBorder);

        // Translators: Label of the box where the user types what to search YouTube for.
        var queryLabel = new StaticText(_dialog, label: Tr("Search for"));
        _query = new TextCtrl(_dialog, value: prompt.InitialQuery);

        // Translators: Label of the list of search suggestions that appears as the user types.
        _suggestionsLabel = new StaticText(_dialog, label: Tr("Suggestions"));
        _suggestions = new ListBox(_dialog, size: new Size(-1, 150));
        _suggestionsLabel.Hide();
        _suggestions.Hide();

        // Translators: Label of the list that narrows a YouTube search to one kind of result.
        var filterLabel = new StaticText(_dialog, label: Tr("Filter"));
        _filter = new Choice(_dialog);
        // Translators: Search filter: return results of every kind, unfiltered.
        _filter.Add(Tr("No filter"));
        // Translators: Search filter: return only live streams.
        _filter.Add(Tr("Live"));
        // Translators: Search filter: order results by when they were uploaded, newest first.
        _filter.Add(Tr("Upload date"));
        // Translators: Search filter: order results by how many times they have been viewed.
        _filter.Add(Tr("View count"));
        // Translators: Search filter: return playlists rather than videos.
        _filter.Add(Tr("Playlist"));
        // Translators: Search filter: return channels rather than videos.
        _filter.Add(Tr("Channels"));
        _filter.SelectedIndex = 0;

        // Translators: Button that runs the YouTube search with the text and filter chosen.
        _search = new Button(_dialog, StandardId.Ok, Tr("Search"));
        _search.Click += (_, _) => Commit();
        _search.Enabled = prompt.InitialQuery.Trim().Length > 0;
        _search.SetDefault();
        // Translators: The button that closes a window.
        var close = new Button(_dialog, StandardId.Cancel, Tr("Close"));

        _debounce = new WxSharp.Timer(_dialog);
        _debounce.Tick += OnDebounceTick;

        BuildLayout(queryLabel, filterLabel, close);
        _query.TextChanged += OnTextChanged;
        _suggestions.ItemActivated += (_, _) => AcceptSuggestion();
        _dialog.Bind(WxEvents.CharHook, OnCharHook);
        _query.Focus();
    }

    /// <summary>Opens the window and returns what the user asked to search for, or null when they closed it
    /// without searching.</summary>
    internal YouTubeSearchRequest? Show()
    {
        _dialog.ShowModal();
        return _result;
    }

    /// <summary>Lays the window out: the query box, the suggestions list that starts hidden, the filter, and
    /// the Search and Close buttons.</summary>
    private void BuildLayout(StaticText queryLabel, StaticText filterLabel, Button close)
    {
        var buttons = new BoxSizer(Orientation.Horizontal);
        buttons.Add(_search, flags: SizerFlags.BorderRight, border: 6);
        buttons.AddStretchSpacer();
        buttons.Add(close);

        var sizer = new BoxSizer(Orientation.Vertical);
        sizer.Add(queryLabel, flags: SizerFlags.All, border: 8);
        sizer.Add(_query, flags: SizerFlags.Expand | SizerFlags.BorderLeft | SizerFlags.BorderRight | SizerFlags.BorderBottom, border: 8);
        sizer.Add(_suggestionsLabel, flags: SizerFlags.BorderLeft | SizerFlags.BorderRight, border: 8);
        sizer.Add(_suggestions, proportion: 1, flags: SizerFlags.Expand | SizerFlags.BorderLeft | SizerFlags.BorderRight | SizerFlags.BorderBottom, border: 8);
        sizer.Add(filterLabel, flags: SizerFlags.BorderLeft | SizerFlags.BorderRight, border: 8);
        sizer.Add(_filter, flags: SizerFlags.Expand | SizerFlags.BorderLeft | SizerFlags.BorderRight | SizerFlags.BorderBottom, border: 8);
        sizer.Add(buttons, flags: SizerFlags.Expand | SizerFlags.All, border: 8);
        _dialog.SetSizer(sizer);
        _dialog.Fit();
        _dialog.MinSize = new Size(480, 220);
        _dialog.Center(onParent: true);
    }

    public void Dispose()
    {
        // Before the native window goes: a fetch still in flight will Post its answer to the UI thread, and
        // that answer must find _closing already set so it returns before reaching the disposed controls.
        _closing = true;
        _debounce.Dispose();
        _dialog.Dispose();
    }

    /// <summary>The text changed: enable Search only for a non-empty query, hide any suggestions from the
    /// last word, and, if suggestions are on, wait for the typing to pause before fetching new ones.</summary>
    private void OnTextChanged(object? sender, CommandEventArgs args)
    {
        var query = _query.Value.Trim();
        _search.Enabled = query.Length > 0;
        HideSuggestions();
        // Every keystroke invalidates whatever the last fetch was for; bumping here means an answer already
        // on its way back is dropped rather than shown against text that has since changed.
        _requestCounter++;
        _debounce.Stop();
        if (_prompt.SuggestionsEnabled && query.Length > 0)
            _debounce.StartOnce(SuggestionsDelayMs);
    }

    /// <summary>The typing has paused: fetch the suggestions for the current query off the UI thread, then
    /// marshal the answer back to be shown.</summary>
    private void OnDebounceTick(object? sender, EventArgs args)
    {
        var query = _query.Value.Trim();
        if (query.Length == 0)
            return;
        var requestId = ++_requestCounter;
        _ = Task.Run(() =>
        {
            IReadOnlyList<string> items;
            try
            {
                items = _prompt.FetchSuggestions(query, CancellationToken.None);
            }
            catch
            {
                // A failed fetch is no suggestions - the box is not the place to report a network hiccup.
                items = Array.Empty<string>();
            }
            _dispatcher.Post(() => OnSuggestionsLoaded(query, items, requestId));
        });
    }

    /// <summary>Shows the fetched suggestions, unless the box has moved on since they were asked for.</summary>
    private void OnSuggestionsLoaded(string query, IReadOnlyList<string> items, int requestId)
    {
        // Three ways the answer is stale: the window is going, a newer fetch has superseded this one, or the
        // user has typed on so the box no longer holds the words these belong to.
        if (_closing || requestId != _requestCounter || _query.Value.Trim() != query)
            return;
        if (items.Count == 0)
        {
            HideSuggestions();
            return;
        }
        _suggestions.Set(items);
        _suggestions.SelectedIndex = -1;
        _suggestionsLabel.Show();
        _suggestions.Show();
        _dialog.Layout();
        _prompt.AnnounceSuggestions();
    }

    private void HideSuggestions()
    {
        if (!_suggestions.Visible)
            return;
        _suggestions.Clear();
        _suggestions.Hide();
        _suggestionsLabel.Hide();
        _dialog.Layout();
    }

    /// <remarks>
    /// The suggestions list is reached and left with the arrows, so those are routed by which control holds
    /// the focus. Escape puts the list away when it is open rather than closing the window; when it is not,
    /// it falls through to the dialog's own cancel.
    /// </remarks>
    private void OnCharHook(object? sender, KeyEventArgs args)
    {
        var focus = Window.FindFocus();
        if (args.Code == Key.Escape && _suggestions.Visible)
        {
            HideSuggestions();
            _query.Focus();
            return;
        }
        if (ReferenceEquals(focus, _query))
        {
            if (args.Code is Key.Down or Key.NumpadDown && _suggestions.Visible && _suggestions.Count > 0)
            {
                _suggestions.SelectedIndex = 0;
                _suggestions.Focus();
                return;
            }
        }
        else if (ReferenceEquals(focus, _suggestions))
        {
            switch (args.Code)
            {
                case Key.Up or Key.NumpadUp when _suggestions.SelectedIndex <= 0:
                    _query.Focus();
                    return;
                case Key.Right or Key.NumpadRight:
                    RefineFromSuggestion();
                    return;
                case Key.Enter or Key.NumpadEnter:
                    AcceptSuggestion();
                    return;
            }
        }
        args.Skip();
    }

    /// <summary>Enter on a suggestion: drop it into the box and search for it at once.</summary>
    private void AcceptSuggestion()
    {
        var index = _suggestions.SelectedIndex;
        if (index < 0)
            return;
        // ChangeValue rather than Value so the programmatic set raises no TextChanged and does not restart
        // the debounce or hide the list out from under the search that follows.
        _query.ChangeValue(_suggestions[index]);
        Commit();
    }

    /// <summary>Right on a suggestion: drop it into the box to be refined, and put the list away.</summary>
    private void RefineFromSuggestion()
    {
        var index = _suggestions.SelectedIndex;
        if (index < 0)
            return;
        _query.ChangeValue(_suggestions[index]);
        _search.Enabled = _query.Value.Trim().Length > 0;
        HideSuggestions();
        _query.Focus();
        _query.MoveCaretToEnd();
    }

    /// <summary>Ends the window naming what to search for. Does nothing for a blank query.</summary>
    private void Commit()
    {
        var query = _query.Value.Trim();
        if (query.Length == 0)
            return;
        _result = new YouTubeSearchRequest(query, Math.Max(0, _filter.SelectedIndex));
        _dialog.EndModal(StandardId.Ok);
    }
}
