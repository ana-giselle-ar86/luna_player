using WxSharp;

namespace LunaPlayer.UI.YouTube;

/// <summary>Offers to fetch the programs YouTube playback and downloads need.</summary>
///
/// <remarks>
/// Shown the first time the user reaches for a YouTube feature without the programs present. There is no
/// "do not ask again": yt-dlp and its runtime are the only route to YouTube, so a refusal is answered by
/// asking again next time rather than by turning the feature off for good.
/// </remarks>
internal sealed class ComponentsDialog : IDisposable
{
    private readonly Dialog _dialog;

    internal ComponentsDialog(Window parent)
    {
        _dialog = new Dialog(
            parent,
            // Translators: Title of the window offering to fetch the extra programs YouTube needs.
            title: Tr("YouTube Components"),
            style: DialogStyle.Default | DialogStyle.ResizeBorder);

        // Translators: Message offering to fetch the extra programs YouTube needs. Playing and saving
        // videos cannot work without them, which is what it says.
        var message = new StaticText(_dialog, label: Tr("Playing and downloading YouTube videos needs some extra programs that are not installed yet. Would you like to download them now?"));
        message.Wrap(420);

        // Translators: Button that agrees to fetch the extra programs YouTube needs.
        var yes = new Button(_dialog, StandardId.Yes, Tr("Yes"));
        yes.SetDefault();
        // Translators: Button that declines to fetch the extra programs YouTube needs.
        var no = new Button(_dialog, StandardId.No, Tr("No"));
        // wxWidgets auto-closes modal dialogs only for OK and Cancel, so Yes and No are bound explicitly.
        yes.Click += (_, _) => _dialog.EndModal(StandardId.Yes);
        no.Click += (_, _) => _dialog.EndModal(StandardId.No);
        // Treat Escape as No because this dialog has no Cancel button.
        _dialog.SetEscapeId(StandardId.No);

        var buttons = new BoxSizer(Orientation.Horizontal);
        buttons.AddStretchSpacer();
        buttons.Add(yes, flags: SizerFlags.BorderRight, border: 8);
        buttons.Add(no);

        var sizer = new BoxSizer(Orientation.Vertical);
        sizer.Add(message, flags: SizerFlags.All | SizerFlags.Expand, border: 10);
        sizer.Add(buttons, flags: SizerFlags.BorderLeft | SizerFlags.BorderRight | SizerFlags.BorderBottom | SizerFlags.Expand, border: 10);
        _dialog.SetSizer(sizer);
        _dialog.Fit();
        _dialog.MinSize = new Size(500, 200);
        _dialog.Center(onParent: true);
        yes.Focus();
    }

    /// <summary>Shows the offer and returns whether the user agreed to fetch the programs.</summary>
    internal bool Show() => _dialog.ShowModal() == StandardId.Yes;

    public void Dispose() => _dialog.Dispose();
}
