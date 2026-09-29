using System.Globalization;
using WxSharp;

namespace LunaPlayer.UI.YouTube;

/// <summary>Asks which of the qualities a video actually offers it should be saved at.</summary>
/// <remarks>
/// Shown only when downloading, and only once yt-dlp has said which qualities the video really has, so
/// every row is a quality the download can honour. The numbers are picture heights for a video download
/// and audio bitrates in kbps for an audio-only one; the words beside them are only there to make the
/// list readable, and the exact number the user picked is what goes back.
/// </remarks>
internal sealed class QualitySelectionDialog : IDisposable
{
    private readonly Dialog _dialog;
    private readonly ListBox _list;
    private readonly IReadOnlyList<int> _options;

    internal QualitySelectionDialog(Window parent, IReadOnlyList<int> options, bool audioOnly)
    {
        _options = options;
        _dialog = new Dialog(
            parent,
            title: audioOnly
                // Translators: Title of the window that picks the audio bitrate a download is saved at.
                ? Tr("Audio quality")
                // Translators: Title of the window that picks the video quality a download is saved at.
                : Tr("Video quality"),
            style: DialogStyle.Default);

        var message = new StaticText(
            _dialog,
            label: audioOnly
                // Translators: Prompt above the list of audio bitrates to choose from when downloading.
                ? Tr("Choose the audio quality to download:")
                // Translators: Prompt above the list of video qualities to choose from when downloading.
                : Tr("Choose the video quality to download:"));

        _list = new ListBox(_dialog);
        foreach (var option in options)
            _list.Add(audioOnly ? AudioLabel(option) : VideoLabel(option));
        if (options.Count > 0)
            _list.SelectedIndex = 0;

        // Translators: The button that confirms a choice.
        var ok = new Button(_dialog, label: Tr("OK"));
        ok.SetDefault();
        ok.Click += (_, _) => _dialog.EndModal(StandardId.Ok);
        // Translators: The button that closes a window and leaves everything as it was.
        var cancel = new Button(_dialog, StandardId.Cancel, Tr("Cancel"));
        _dialog.SetEscapeId(StandardId.Cancel);
        _list.ItemActivated += (_, _) => _dialog.EndModal(StandardId.Ok);

        var buttons = new BoxSizer(Orientation.Horizontal);
        buttons.AddStretchSpacer();
        buttons.Add(ok, flags: SizerFlags.BorderRight, border: 6);
        buttons.Add(cancel);

        var sizer = new BoxSizer(Orientation.Vertical);
        sizer.Add(message, flags: SizerFlags.All, border: 10);
        sizer.Add(_list, proportion: 1, flags: SizerFlags.Expand | SizerFlags.BorderLeft | SizerFlags.BorderRight, border: 10);
        sizer.Add(buttons, flags: SizerFlags.Expand | SizerFlags.All, border: 10);
        _dialog.SetSizer(sizer);
        _dialog.Fit();
        _dialog.MinSize = new Size(360, 300);
        _dialog.Center(onParent: true);
        _list.Focus();
    }

    /// <summary>The quality the user chose, as the exact number the list was built from, or null when they
    /// backed out.</summary>
    internal int? Show()
    {
        if (_dialog.ShowModal() != StandardId.Ok)
            return null;
        var index = _list.SelectedIndex;
        return index >= 0 && index < _options.Count ? _options[index] : null;
    }

    public void Dispose() => _dialog.Dispose();

    /// <summary>The words shown beside an audio bitrate. Shared with the preferences page, which offers the
    /// same bitrates.</summary>
    internal static string AudioLabel(int kbps)
        // Translators: One audio-bitrate choice. {rate} is a number of kilobits per second.
        => TrFormat("{rate} kbps", kbps.ToString(CultureInfo.InvariantCulture));

    /// <summary>The words shown beside a picture height, matching the Python player's wording, with a plain
    /// "{height}p" for any height not in the table. Shared with the preferences page.</summary>
    internal static string VideoLabel(int height)
    {
        var number = height.ToString(CultureInfo.InvariantCulture);
        return height switch
        {
            // Translators: Description of the 144p video quality.
            144 => Tr("144p (very low quality)"),
            // Translators: Description of the 240p video quality.
            240 => Tr("240p (low quality)"),
            // Translators: Description of the 360p video quality.
            360 => Tr("360p (medium quality)"),
            // Translators: Description of the 480p video quality.
            480 => Tr("480p (medium quality)"),
            // Translators: Description of the 720p video quality (high definition).
            720 => Tr("720p (HD)"),
            // Translators: Description of the 1080p video quality (full high definition).
            1080 => Tr("1080p (Full HD)"),
            // Translators: Description of the 1440p video quality (2K).
            1440 => Tr("1440p (2K)"),
            // Translators: Description of the 2160p video quality (4K).
            2160 => Tr("2160p (4K)"),
            // Translators: A video quality with no set description. {height} is a number of pixels.
            _ => TrFormat("{height}p", number),
        };
    }
}
