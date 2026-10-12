using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using LunaPlayer.Application;
using WxSharp;
using Downloads = LunaPlayer.YouTube.Download;

namespace LunaPlayer.UI.YouTube;

/// <summary>Modal progress window for a download: text block, progress bar, and a confirm-first Cancel. The
/// download runs on a worker; a timer drains its reports. The caller shows the Success/Error message.</summary>
internal sealed class DownloadProgressDialog : IDisposable
{
    private const int Range = 1000;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(150);

    private readonly Dialog _dialog;
    private readonly IApplicationDispatcher _dispatcher;
    private readonly TextCtrl _text;
    private readonly Gauge _gauge;
    private readonly ConcurrentQueue<Downloads.Progress> _updates = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Func<Action<Downloads.Progress>, CancellationToken, Downloads.Outcome> _work;

    private Task<Downloads.Outcome>? _task;
    private IDisposable? _ticker;
    private Downloads.Outcome? _outcome;
    private bool _finished;
    private bool _confirming;

    internal DownloadProgressDialog(
        Window parent,
        IApplicationDispatcher dispatcher,
        Func<Action<Downloads.Progress>, CancellationToken, Downloads.Outcome> work)
    {
        _dispatcher = dispatcher;
        _work = work;
        // Translators: Title of the window shown while a video is being downloaded.
        _dialog = new Dialog(parent, title: Tr("Downloading"), style: DialogStyle.Default | DialogStyle.ResizeBorder);

        var sizer = new BoxSizer(Orientation.Vertical);
        _text = new TextCtrl(
            _dialog,
            // Translators: Shown in the download window before the first progress has arrived.
            value: Tr("Preparing to download…"),
            style: TextCtrlStyle.MultiLine | TextCtrlStyle.ReadOnly | TextCtrlStyle.DontWrap);
        sizer.Add(_text, proportion: 1, flags: SizerFlags.All | SizerFlags.Expand, border: 8);
        _gauge = new Gauge(_dialog, range: Range);
        sizer.Add(_gauge, flags: SizerFlags.BorderLeft | SizerFlags.BorderRight | SizerFlags.BorderBottom | SizerFlags.Expand, border: 8);
        // A plain button, not the standard Cancel, so Escape and clicks route through the confirmation.
        // Translators: The button that stops a running download. It asks for confirmation first.
        var cancel = new Button(_dialog, label: Tr("Cancel"));
        cancel.Click += (_, _) => RequestStop();
        sizer.Add(cancel, flags: SizerFlags.BorderLeft | SizerFlags.BorderRight | SizerFlags.BorderBottom | SizerFlags.AlignRight, border: 8);

        _dialog.SetSizer(sizer);
        _dialog.Fit();
        _dialog.MinSize = new Size(520, 300);
        _dialog.Center(onParent: true);
        _dialog.Bind(WxEvents.CharHook, OnCharHook);
        _dialog.Closing += (_, args) =>
        {
            args.Veto();
            RequestStop();
        };
        cancel.Focus();
    }

    /// <summary>Runs the download behind the window; returns the outcome, or null when the user stopped it.</summary>
    internal Downloads.Outcome? Show()
    {
        _task = Task.Run(() => _work(_updates.Enqueue, _cancellation.Token), _cancellation.Token);
        _ticker = _dispatcher.Repeat(TickInterval, Tick);
        _dialog.ShowModal();
        return _outcome;
    }

    public void Dispose()
    {
        _ticker?.Dispose();
        _ticker = null;
        _cancellation.Dispose();
        _dialog.Dispose();
    }

    private void Tick()
    {
        if (_finished || _confirming)
            return;
        Downloads.Progress? latest = null;
        while (_updates.TryDequeue(out var update))
            latest = update;
        if (latest is Downloads.Progress shown)
            Render(shown);
        if (_task!.IsCompleted)
            Finish();
    }

    private void Render(Downloads.Progress progress)
    {
        _gauge.Value = Math.Clamp((int)Math.Round(progress.Percent * (Range / 100.0)), 0, Range);
        _text.Value = string.Join(Environment.NewLine,
            // Translators: How much of the file has downloaded. {done} and {total} are sizes such as "4.2 MB".
            TrFormat("Downloaded: {done} of {total}",
                Size(progress.DownloadedBytes),
                progress.TotalBytes > 0
                    ? Size(progress.TotalBytes)
                    // Translators: Shown for the total size before yt-dlp has reported it.
                    : Tr("unknown")),
            // Translators: How far the download has got. {percent} is a whole number.
            TrFormat("Percentage: {percent}%", (int)Math.Round(progress.Percent)),
            // Translators: The current download speed. {speed} is a size per second such as "1.3 MB/s".
            TrFormat("Speed: {speed}",
                progress.Speed > 0
                    ? TrFormat("{size}/s", Size((long)progress.Speed))
                    // Translators: Shown for the speed before enough has downloaded to measure it.
                    : Tr("calculating…")),
            // Translators: A guess at the time left. {time} is a clock such as 3:20, or the word for a guess
            // not yet worth making.
            TrFormat("Estimated time left: {time}",
                progress.Eta > 0
                    ? FormatDuration(TimeSpan.FromSeconds(progress.Eta))
                    // Translators: Shown for the estimated time left before enough is done to guess it.
                    : Tr("calculating…")));
    }

    private void RequestStop()
    {
        if (_finished || _confirming)
            return;
        _confirming = true;
        var stop = Wx.MessageBox(
            // Translators: Asks the user to confirm stopping a download that is still running.
            Tr("The download is still running. Would you like to stop it?"),
            // Translators: Title of the prompt shown when the user tries to stop a running download.
            Tr("Warning"), MessageBoxStyle.YesNo | MessageBoxStyle.IconWarning, _dialog) == MessageBoxStyle.Yes;
        _confirming = false;
        if (stop && !_task!.IsCompleted)
            _cancellation.Cancel();
    }

    private void Finish()
    {
        _finished = true;
        var ticker = _ticker;
        _ticker = null;
        _dispatcher.Post(() =>
        {
            ticker?.Dispose();
            _dialog.EndModal(StandardId.Ok);
            if (_task!.IsCompletedSuccessfully)
            {
                _outcome = _task.Result;
                return;
            }
            // A cancelled download reports nothing; a genuine fault is rethrown rather than hidden.
            if (_task.IsFaulted && _task.Exception?.InnerException is Exception failure
                and not OperationCanceledException)
                ExceptionDispatchInfo.Capture(failure).Throw();
        });
    }

    private void OnCharHook(object? sender, KeyEventArgs args)
    {
        if (args.Code == Key.Escape)
            RequestStop();
        else
            args.Skip();
    }

    /// <summary>A count of bytes in the largest unit that keeps it readable.</summary>
    private static string Size(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";
    }
}

