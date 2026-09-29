using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LunaPlayer.Configuration;

namespace LunaPlayer.YouTube;

/// <summary>What one run of yt-dlp produced.</summary>
/// <param name="Lines">Its output, blank lines dropped.</param>
/// <param name="Diagnostic">The first line of its complaint, when it failed. Empty when it did not.</param>
internal readonly record struct YtDlpRun(IReadOnlyList<string> Lines, string Diagnostic)
{
    internal bool Failed => Diagnostic.Length > 0;
}

/// <summary>Talks to yt-dlp.</summary>
///
/// <remarks>
/// Optional fallback resolver and downloader. Its operations wait for an external process and must run on
/// a worker thread.
/// </remarks>
internal sealed partial class YtDlpClient
{
    private readonly PlayerSettings _settings;

    /// <param name="settings">Read live on each run, so a cookie source chosen in preferences takes effect
    /// on the next yt-dlp call without the client being rebuilt.</param>
    internal YtDlpClient(PlayerSettings settings) => _settings = settings;

    /// <summary>The yt-dlp format selector for sound alone, capped at a bitrate in kbps.</summary>
    private static string AudioFormat(int abr)
        => $"bestaudio[abr<=?{abr}][ext=m4a]/bestaudio[abr<=?{abr}]/bestaudio[ext=m4a]/bestaudio/best";

    /// <summary>The yt-dlp format selector for a video capped at a picture height.</summary>
    private static string VideoFormat(int height)
        => $"best[height<=?{height}][ext=mp4]/best[height<=?{height}]/best[ext=mp4]/best";

    /// <summary>The format selector for a play: sound alone capped at a bitrate, or picture capped at a
    /// height. The number is a bitrate in kbps when <paramref name="audioOnly"/>, a height otherwise.</summary>
    internal static string Format(bool audioOnly, int quality)
        => audioOnly ? AudioFormat(quality) : VideoFormat(quality);

    /// <summary>Turns a video into something playable.</summary>
    ///
    /// <remarks>
    /// Attempts JSON metadata first, then a formatted direct URL, and finally an unrestricted direct URL.
    /// Later attempts provide less control over the selected format.
    /// </remarks>
    internal ResolveOutcome Resolve(
        string watchUrl, YouTubeResult item, bool audioOnly, int quality, CancellationToken token)
    {
        // Deno is required as well as yt-dlp; without it, returned stream URLs can be severely throttled.
        if (!Tools.HasAll)
            return ResolveOutcome.Failed(ResolveFailure.MissingComponents);
        var format = Format(audioOnly, quality);
        var diagnostic = string.Empty;
        try
        {
            var full = Run(["--no-playlist", "--dump-single-json", "-f", format, watchUrl], token);
            if (RateLimited(full))
                return ResolveOutcome.Failed(ResolveFailure.RateLimited, full.Diagnostic);
            if (!full.Failed && Parse(full.Lines) is JsonElement data)
            {
                var described = Describe(data, watchUrl, item);
                if (PickStream(data) is string address)
                    return Ready(described, address);
                item = described;
            }
            diagnostic = full.Diagnostic;

            var formatted = Run(["--no-playlist", "-g", "-f", format, watchUrl], token);
            if (RateLimited(formatted))
                return ResolveOutcome.Failed(ResolveFailure.RateLimited, formatted.Diagnostic);
            if (formatted.Lines.Count > 0)
                return Ready(item, formatted.Lines[0]);
            diagnostic = formatted.Diagnostic.Length > 0 ? formatted.Diagnostic : diagnostic;

            var bare = Run(["--no-playlist", "-g", watchUrl], token);
            if (RateLimited(bare))
                return ResolveOutcome.Failed(ResolveFailure.RateLimited, bare.Diagnostic);
            if (bare.Lines.Count > 0)
                return Ready(item, bare.Lines[0]);
            diagnostic = bare.Diagnostic.Length > 0 ? bare.Diagnostic : diagnostic;
        }
        catch (OperationCanceledException)
        {
            return ResolveOutcome.Cancelled;
        }
        catch (Exception failure)
        {
            return ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message);
        }
        return ResolveOutcome.Failed(ResolveFailure.NoStream, diagnostic);
    }

    /// <summary>The details of one video, for a link the user gave rather than chose from a list.</summary>
    internal YouTubeResult? Video(string watchUrl, CancellationToken token)
    {
        var run = Run(["--no-playlist", "--dump-single-json", watchUrl], token);
        return Parse(run.Lines) is JsonElement data ? Describe(data, watchUrl, YouTubeResult.None) : null;
    }

    /// <summary>The text the uploader wrote under a video, or null when it could not be read.</summary>
    internal string? Description(string watchUrl, CancellationToken token)
    {
        var run = Run(["--no-playlist", "--dump-single-json", watchUrl], token);
        return Parse(run.Lines) is JsonElement data && Text(data, "description") is string text && text.Length > 0
            ? text
            : null;
    }

    /// <summary>Every video in a playlist, and what the playlist is called.</summary>
    internal (string Title, IReadOnlyList<YouTubeResult> Items)? Playlist(string link, CancellationToken token)
    {
        var run = Run(["--flat-playlist", "--dump-single-json", "--ignore-errors", link], token);
        if (Parse(run.Lines) is not JsonElement data)
            return null;
        var items = new List<YouTubeResult>();
        if (data.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object && Entry(entry) is YouTubeResult found)
                    items.Add(found);
            }
        }
        return (Text(data, "title") ?? string.Empty, items);
    }

    /// <summary>One window of rows from a channel tab, and how many raw entries that window held.</summary>
    /// <remarks>
    /// <paramref name="start"/> and <paramref name="end"/> are 1-based and inclusive, the way Hex Player's
    /// <c>playliststart</c>/<c>playlistend</c> are. The raw count is returned alongside the mapped rows
    /// because the paging decision - whether more windows remain - is about how much the tab held, not how
    /// much of it was playable: a window can be full of community posts that map to nothing and still not be
    /// the last one. A failed run with nothing to say is treated as the end of the tab rather than an error,
    /// because <c>--ignore-errors</c> lets yt-dlp finish a partial tab.
    /// </remarks>
    internal (IReadOnlyList<YouTubeResult> Items, int RawCount) ChannelTab(
        string channelBase, string tabKey, int start, int end, CancellationToken token)
    {
        var url = ChannelTabs.TabUrl(channelBase, tabKey);
        var run = Run(
            ["--flat-playlist", "--dump-single-json", "--ignore-errors", "-I", $"{start}:{end}", url],
            token);
        if (Parse(run.Lines) is not JsonElement data)
        {
            if (run.Failed)
                throw new InvalidOperationException(run.Diagnostic);
            return ([], 0);
        }
        var items = new List<YouTubeResult>();
        var raw = 0;
        if (data.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;
                raw++;
                if (ChannelEntry(entry, tabKey) is YouTubeResult found)
                    items.Add(found);
            }
        }
        return (items, raw);
    }

    /// <summary>The distinct qualities a video actually offers, best first, for the download picker.</summary>
    /// <remarks>
    /// Video mode returns the picture heights (720, 1080...); audio mode the audio bitrates in kbps. The
    /// numbers come straight from yt-dlp's own format list, so the picker can only ever offer what the video
    /// has. An empty list means yt-dlp said nothing usable, and the caller falls back to the settings
    /// quality rather than showing an empty picker.
    /// </remarks>
    internal IReadOnlyList<int> AvailableQualities(string watchUrl, bool audioOnly, CancellationToken token)
    {
        var run = Run(["--no-playlist", "--dump-single-json", "--no-warnings", watchUrl], token);
        if (Parse(run.Lines) is not JsonElement data
            || !data.TryGetProperty("formats", out var formats)
            || formats.ValueKind != JsonValueKind.Array)
            return [];
        var found = new SortedSet<int>();
        foreach (var format in formats.EnumerateArray())
        {
            if (format.ValueKind != JsonValueKind.Object)
                continue;
            if (audioOnly)
            {
                // An audio format carries sound and no picture; its bitrate rounds to the nearest kbps.
                if (Has(format, "acodec") && !Has(format, "vcodec")
                    && Number(format, "abr") is double abr && abr > 0)
                    found.Add((int)Math.Round(abr));
            }
            // A video format carries a picture; its height names the quality.
            else if (Has(format, "vcodec") && Number(format, "height") is double height && height > 0)
                found.Add((int)Math.Round(height));
        }
        // SortedSet is ascending; the picker wants the best first.
        return found.Reverse().ToArray();
    }

    /// <summary>Saves a video into <paramref name="folder"/>, reporting as the bytes arrive.</summary>
    ///
    /// <remarks>
    /// yt-dlp chooses the output name from its template. Progress is parsed from its line-oriented output.
    /// </remarks>
    /// <param name="report">The name being written, the bytes so far and the bytes expected. Called from
    /// the thread this runs on.</param>
    internal void Download(
        string watchUrl,
        string folder,
        bool audioOnly,
        int quality,
        Action<string, long, long> report,
        CancellationToken token,
        int? exactQuality = null)
    {
        var arguments = new List<string>
        {
            "--newline",
            "--progress",
            "--no-warnings",
            "--no-playlist",
        };
        // The quality picker's exact pick wins over the settings quality; both are plain numbers now - a
        // bitrate in kbps for sound, a picture height for video.
        var chosen = exactQuality ?? quality;
        if (audioOnly)
            arguments.AddRange(["-x", "--audio-format", "m4a", "--audio-quality", $"{chosen}K"]);
        else
            arguments.AddRange(["-f", VideoFormat(chosen)]);
        arguments.AddRange(["-o", Path.Combine(folder, "%(title)s.%(ext)s")]);
        if (Tools.DenoRuntime is string runtime)
            arguments.AddRange(["--js-runtimes", runtime]);
        arguments.Add(watchUrl);

        var name = string.Empty;
        var run = Run(arguments, token, line =>
        {
            if (Destination().Match(line) is { Success: true } destination)
                name = Path.GetFileName(destination.Groups["path"].Value.Trim());
            if (Progress().Match(line) is not { Success: true } progress)
                return;
            var percent = double.Parse(progress.Groups["pct"].Value, CultureInfo.InvariantCulture);
            var total = Bytes(progress.Groups["size"].Value);
            report(name, (long)(total * percent / 100.0), total);
        });
        if (run.Failed)
            throw new InvalidOperationException(run.Diagnostic);
    }

    /// <summary>The version of the yt-dlp beside the player, or an empty string when there is none.
    /// </summary>
    internal string Version(CancellationToken token)
    {
        if (!Tools.HasYtDlp)
            return string.Empty;
        var run = Run(["--version"], token, timeout: TimeSpan.FromSeconds(60));
        return run.Failed || run.Lines.Count == 0 ? string.Empty : CleanVersion(run.Lines[0]);
    }

    /// <summary>Has yt-dlp replace itself with the newest build on a channel.</summary>
    ///
    /// <remarks>
    /// Uses yt-dlp's updater because it can replace its running executable on Windows.
    /// </remarks>
    /// <param name="report">Each line yt-dlp prints, so the window shows what it is doing.</param>
    internal (string Before, string After, bool Updated) SelfUpdate(
        YtDlpChannel channel, Action<string> report, CancellationToken token)
    {
        if (!Tools.HasYtDlp)
            throw new InvalidOperationException("yt-dlp is not available.");
        var before = Version(token);
        var name = ChannelName(channel);
        var run = Run(["--update-to", $"{name}@latest"], token, report, TimeSpan.FromMinutes(5));
        if (run.Failed)
            throw new InvalidOperationException(run.Diagnostic);
        var after = Version(token);
        var updated = after.Length > 0 && before.Length > 0 && !string.Equals(before, after, StringComparison.Ordinal);
        if (!updated)
        {
            // A first install through --update-to reports no version change, because there was no version
            // before it. Its own words are the only evidence that something happened.
            updated = run.Lines.Any(line =>
                line.Contains("Updated yt-dlp to", StringComparison.OrdinalIgnoreCase));
        }
        return (before, after.Length > 0 ? after : before, updated);
    }

    internal static string ChannelName(YtDlpChannel channel) => channel switch
    {
        YtDlpChannel.Nightly => "nightly",
        YtDlpChannel.Master => "master",
        _ => "stable",
    };

    /// <summary>The repository each channel is built from.</summary>
    internal static string ChannelRepository(YtDlpChannel channel) => channel switch
    {
        YtDlpChannel.Nightly => "yt-dlp/yt-dlp-nightly-builds",
        YtDlpChannel.Master => "yt-dlp/yt-dlp-master-builds",
        _ => "yt-dlp/yt-dlp",
    };

    // ---- running it ----

    private YtDlpRun Run(
        IEnumerable<string> arguments,
        CancellationToken token,
        Action<string>? onLine = null,
        TimeSpan? timeout = null)
    {
        var all = new List<string> { "--no-warnings", "--extractor-args", "youtube:player_client=android" };
        all.AddRange(arguments);
        // Only added when it is not already there: the download path puts it in itself, because it builds
        // its own argument list rather than going through the common prefix.
        if (Tools.DenoRuntime is string runtime && !all.Contains("--js-runtimes"))
            all.AddRange(["--js-runtimes", runtime]);
        AddCookies(all);

        using var process = Tools.Start(Tools.YtDlpPath, all);
        // Killed the moment the token is set rather than at the next line of output. yt-dlp can sit for a
        // long time saying nothing - a slow site, a retry, a stalled connection - and a Cancel button that
        // only answers when the program next speaks is a Cancel button that does not work.
        using var abort = token.Register(() => Stop(process));
        var lines = new List<string>();
        var errors = new List<string>();
        // Read on a thread of its own. A program that fills one pipe while nothing drains the other stops
        // there for good, and yt-dlp writes a great deal to both.
        var reading = Task.Run(() =>
        {
            string? line;
            while ((line = process.StandardError.ReadLine()) is not null)
                errors.Add(line.Trim());
        }, CancellationToken.None);
        try
        {
            string? line;
            while ((line = process.StandardOutput.ReadLine()) is not null)
            {
                token.ThrowIfCancellationRequested();
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;
                onLine?.Invoke(trimmed);
                lines.Add(trimmed);
            }
            var span = timeout ?? TimeSpan.FromMinutes(3);
            if (!process.WaitForExit((int)span.TotalMilliseconds))
                throw new TimeoutException($"yt-dlp did not finish within {span.TotalSeconds:F0} seconds.");
            // A killed process exits like any other, so the abort is reported here rather than left to look
            // like a program that failed.
            token.ThrowIfCancellationRequested();
        }
        catch (Exception)
        {
            Stop(process);
            throw;
        }
        finally
        {
            reading.Wait(TimeSpan.FromSeconds(2));
        }
        if (process.ExitCode == 0)
            return new YtDlpRun(lines, string.Empty);
        return new YtDlpRun([], ShortDiagnostic(errors) is { Length: > 0 } complaint
            ? complaint
            : ShortDiagnostic(lines) is { Length: > 0 } fallback
                ? fallback
                : $"yt-dlp exited with code {process.ExitCode}.");
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // It finished between the test and the kill, which is the outcome that was wanted.
        }
    }

    /// <summary>Adds the cookie source the user chose, if any, to a yt-dlp argument list.</summary>
    /// <remarks>
    /// The two sources are mutually exclusive - the preferences page enforces that - so at most one of these
    /// is ever added. A file that no longer exists is silently skipped rather than handed to yt-dlp, which
    /// would fail the run outright. This one insertion point covers resolving, downloading, descriptions,
    /// playlists and the channel tabs, because every one of them routes through <see cref="Run"/>.
    /// </remarks>
    private void AddCookies(List<string> arguments)
    {
        if (_settings.YouTube.CookiesFromFirefox)
        {
            arguments.AddRange(["--cookies-from-browser", "firefox"]);
            return;
        }
        var path = _settings.YouTube.CookiesPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            arguments.AddRange(["--cookies", path]);
    }

    // ---- reading what it said ----

    private static ResolveOutcome Ready(YouTubeResult item, string address)
        => ResolveOutcome.Ok(new Resolved(
            item.Url.Length > 0 ? item : item with { Url = address },
            address,
            // yt-dlp is asked for one address, not a pair. Its format strings prefer the streams that
            // carry sound and picture together, so there is never a second one to go with it.
            null,
            StreamPicker.ExpiryOf(address)));

    private static bool RateLimited(YtDlpRun run)
        => run.Diagnostic.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase)
            || run.Diagnostic.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);

    private static JsonElement? Parse(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return null;
        try
        {
            using var document = JsonDocument.Parse(string.Join('\n', lines));
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not a failure worth its own path: every caller treats "no data" and "unreadable data" the
            // same, and the message they show says which of their own jobs did not finish.
            return null;
        }
    }

    /// <summary>The address to play, out of everything yt-dlp said about a video.</summary>
    private static string? PickStream(JsonElement data)
    {
        if (Text(data, "url") is { Length: > 0 } direct)
            return direct;
        // A format that needs joining is reported as parts. This playback path cannot join separate live
        // streams, so use the first part, which carries the picture.
        if (!data.TryGetProperty("requested_formats", out var parts) || parts.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object && Text(part, "url") is { Length: > 0 } address)
                return address;
        }
        return null;
    }

    /// <summary>What yt-dlp says about a video, falling back to what the caller already knew.</summary>
    private static YouTubeResult Describe(JsonElement data, string watchUrl, YouTubeResult known)
    {
        var found = Entry(data);
        if (found is not YouTubeResult item)
            return known.Url.Length > 0 ? known : known with { Url = watchUrl, Title = watchUrl };
        return known.Url.Length > 0 ? item with { Url = known.Url } : item;
    }

    private static YouTubeResult? Entry(JsonElement entry)
    {
        var url = Text(entry, "webpage_url") ?? string.Empty;
        var id = Text(entry, "id") ?? string.Empty;
        if (url.Length == 0)
        {
            var raw = Text(entry, "url") ?? string.Empty;
            url = raw.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? raw
                : id.Length > 0
                    ? PyYtClient.WatchUrl(id)
                    : raw.Length > 0 ? PyYtClient.WatchUrl(raw) : string.Empty;
        }
        if (url.Length == 0)
            return null;
        // Spelled the one way the player spells them, so an address from here and one from a search name
        // the same cache entry and the same playlist source.
        url = PyYtClient.Canonical(url) ?? url;
        var duration = entry.TryGetProperty("duration", out var seconds)
            && seconds.ValueKind == JsonValueKind.Number
            && seconds.TryGetDouble(out var value) && value > 0
                ? TimeSpan.FromSeconds(value)
                : (TimeSpan?)null;
        return new YouTubeResult(
            id,
            Text(entry, "title") is { Length: > 0 } title ? title : url,
            Text(entry, "channel") ?? Text(entry, "uploader") ?? string.Empty,
            duration,
            url,
            Text(entry, "channel_url") ?? Text(entry, "uploader_url") ?? string.Empty);
    }

    /// <summary>One row of a channel tab, tagged with what kind of thing it is so the browser knows whether
    /// to play it, page into a playlist or open another channel.</summary>
    /// <remarks>
    /// The tab a row came from settles its kind for the tabs that hold only one - the videos, shorts and
    /// streams tabs are all videos, playlists are playlists, channels are channels. Only the mixed home and
    /// community tabs fall back to reading the row's own shape. A playlist or channel row keeps its own list
    /// or channel address rather than being canonicalised to a watch URL, which would name nothing.
    /// </remarks>
    private static YouTubeResult? ChannelEntry(JsonElement entry, string tabKey)
    {
        var id = Text(entry, "id") ?? string.Empty;
        var rawUrl = Text(entry, "url") ?? Text(entry, "webpage_url") ?? string.Empty;
        var type = ChannelItemType(entry, tabKey, rawUrl);
        if (type is YouTubeItemType.Video)
            return Entry(entry) is YouTubeResult video ? video with { ItemType = YouTubeItemType.Video } : null;

        var url = type is YouTubeItemType.Playlist ? PlaylistUrl(id, rawUrl) : ChannelUrl(id, rawUrl);
        if (url.Length == 0)
            return null;
        return new YouTubeResult(
            id,
            Text(entry, "title") is { Length: > 0 } title ? title : url,
            Text(entry, "channel") ?? Text(entry, "uploader") ?? string.Empty,
            null,
            url,
            Text(entry, "channel_url") ?? Text(entry, "uploader_url") ?? string.Empty)
        {
            ItemType = type,
        };
    }

    private static YouTubeItemType ChannelItemType(JsonElement entry, string tabKey, string rawUrl)
    {
        // The single-kind tabs answer for every row in them.
        if (tabKey is "videos" or "shorts" or "streams")
            return YouTubeItemType.Video;
        if (tabKey is "playlists")
            return YouTubeItemType.Playlist;
        if (tabKey is "channels")
            return YouTubeItemType.Channel;
        var tag = Text(entry, "_type");
        if (string.Equals(tag, "playlist", StringComparison.OrdinalIgnoreCase)
            || rawUrl.Contains("list=", StringComparison.Ordinal)
            || rawUrl.Contains("/playlist", StringComparison.OrdinalIgnoreCase))
            return YouTubeItemType.Playlist;
        if (string.Equals(tag, "channel", StringComparison.OrdinalIgnoreCase)
            || rawUrl.Contains("/channel/", StringComparison.OrdinalIgnoreCase)
            || rawUrl.Contains("/@", StringComparison.Ordinal))
            return YouTubeItemType.Channel;
        return YouTubeItemType.Video;
    }

    private static string PlaylistUrl(string id, string rawUrl)
    {
        if (rawUrl.Contains("list=", StringComparison.Ordinal)
            || rawUrl.Contains("/playlist", StringComparison.OrdinalIgnoreCase))
            return rawUrl;
        return id.Length > 0 ? $"https://www.youtube.com/playlist?list={id}" : rawUrl;
    }

    private static string ChannelUrl(string id, string rawUrl)
    {
        if (rawUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return rawUrl;
        return id.Length > 0 ? $"https://www.youtube.com/channel/{id}" : rawUrl;
    }

    private static string? Text(JsonElement value, string name)
        => value.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()?.Trim()
            : null;

    /// <summary>Reads a number, whether yt-dlp wrote it as a JSON number or as a numeric string.</summary>
    private static double? Number(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var found))
            return null;
        if (found.ValueKind == JsonValueKind.Number && found.TryGetDouble(out var number))
            return number;
        return found.ValueKind == JsonValueKind.String
            && double.TryParse(found.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    /// <summary>Whether a format names a real codec for a track, rather than yt-dlp's "none".</summary>
    private static bool Has(JsonElement format, string codec)
        => Text(format, codec) is { Length: > 0 } value
            && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);

    private static string ShortDiagnostic(IReadOnlyList<string> lines)
    {
        var first = lines.FirstOrDefault(line => line.Length > 0)?.Trim() ?? string.Empty;
        return first.Length <= 220 ? first : string.Concat(first.AsSpan(0, 220).TrimEnd(), "...");
    }

    /// <summary>Drops the program's own name from the front of a version string, as it prints it.</summary>
    private static string CleanVersion(string text)
    {
        var line = text.Trim();
        return line.StartsWith("yt-dlp", StringComparison.OrdinalIgnoreCase)
            ? line["yt-dlp".Length..].Trim(' ', ':', '-')
            : line;
    }

    /// <summary>A size as yt-dlp prints it - "12.34MiB", "1.2GB" - in bytes.</summary>
    private static long Bytes(string text)
    {
        var match = Size().Match(text.Trim());
        if (!match.Success)
            return 0;
        var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        var multiplier = match.Groups["unit"].Value.ToUpperInvariant() switch
        {
            "KB" => 1000L,
            "MB" => 1000L * 1000,
            "GB" => 1000L * 1000 * 1000,
            "TB" => 1000L * 1000 * 1000 * 1000,
            "KIB" => 1024L,
            "MIB" => 1024L * 1024,
            "GIB" => 1024L * 1024 * 1024,
            "TIB" => 1024L * 1024 * 1024 * 1024,
            _ => 1L,
        };
        return (long)(value * multiplier);
    }

    [GeneratedRegex(@"\[download\]\s+(?<pct>\d+(?:\.\d+)?)%\s+of\s+~?\s*(?<size>\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex Progress();

    /// <summary>Either line yt-dlp writes naming a file it is about to produce.</summary>
    /// <remarks>
    /// Both, because an audio-only save writes two. The download names the container it fetched - an mp4 -
    /// and the extraction step then names the m4a that is actually left on disk, which is the name the user
    /// is waiting for. Matching only the first reports a file that will not be there at the end.
    /// </remarks>
    [GeneratedRegex(@"^\[(?:download|ExtractAudio)\] Destination:\s*(?<path>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Destination();

    [GeneratedRegex(@"^(?<value>[0-9]+(?:\.[0-9]+)?)\s*(?<unit>[KMGT]?i?B)$", RegexOptions.IgnoreCase)]
    private static partial Regex Size();
}
