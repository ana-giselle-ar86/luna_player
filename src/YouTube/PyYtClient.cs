using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PyYt;

namespace LunaPlayer.YouTube;

/// <summary>A page of search results that pages forward on demand.</summary>
///
/// <remarks>
/// PyYt's search objects are stateful: each call to <see cref="SearchBase.NextAsync"/> returns the next
/// batch and re-derives the continuation token, and <see cref="SearchBase.HasMoreResults"/> only tells the
/// truth after the first call has run. This wrapper keeps the object alive across the pages Luna's results
/// feed asks for, hands back neutral <see cref="YouTubeResult"/> rows, and serialises the calls behind a
/// gate because the feed can ask for more before the previous ask has returned. It keeps the same name and
/// shape as the paging enumerator the previous client exposed, so the session layer did not have to change.
/// </remarks>
internal sealed class SearchPage : IAsyncDisposable
{
    private readonly SearchBase _search;
    private readonly SemaphoreSlim _turn = new(1, 1);
    // False until the first NextAsync has run, because HasMoreResults cannot be trusted before then: a
    // brand-new search has no continuation and would otherwise look exhausted before it had fetched a thing.
    private bool _started;
    private bool _exhausted;

    internal SearchPage(SearchBase search) => _search = search;

    internal bool HasMore => !_exhausted;

    internal async Task<IReadOnlyList<YouTubeResult>> Take(int count, CancellationToken token)
    {
        var found = new List<YouTubeResult>();
        await _turn.WaitAsync(token).ConfigureAwait(false);
        try
        {
            while (found.Count < count && !_exhausted)
            {
                token.ThrowIfCancellationRequested();
                if (_started && !_search.HasMoreResults)
                {
                    _exhausted = true;
                    break;
                }
                _started = true;
                JsonObject page = await _search.NextAsync(token).ConfigureAwait(false);
                int before = found.Count;
                if (page["result"] is JsonArray results)
                    foreach (JsonNode? node in results)
                        if (node is JsonObject item && PyYtClient.ToResult(item) is YouTubeResult row)
                            found.Add(row);
                // A page that added nothing and left no continuation is the end; stop rather than spin.
                if (found.Count == before && !_search.HasMoreResults)
                    _exhausted = true;
            }
        }
        finally
        {
            _turn.Release();
        }
        return found;
    }

    public async ValueTask DisposeAsync()
    {
        // Non-cancellable: the object has to be disposed even when the page is being torn down because the
        // work was abandoned, and the gate makes sure a page still being read is left alone until it is done.
        await _turn.WaitAsync().ConfigureAwait(false);
        try
        {
            _search.Dispose();
        }
        finally
        {
            _turn.Release();
        }
    }
}

/// <summary>Everything Luna asks of YouTube that is not the playable stream itself: searching, and the
/// title/author/duration/description of a video, playlist, or channel.</summary>
///
/// <remarks>
/// Backed by PyYt, which reads YouTube's own web endpoints and hands back JSON. It cannot decipher a
/// playable stream URL - that is yt-dlp's job now, and the only job it has - so there is deliberately no
/// Resolve or Download here. The library's public surface returns <see cref="JsonObject"/> trees and keeps
/// its navigation helpers to itself, so this client walks the trees with its own small helpers and maps
/// them onto the neutral <see cref="YouTubeResult"/> the rest of the player speaks in.
///
/// PyYt is async throughout; the player drives it from background jobs and bridges to it synchronously with
/// <see cref="Wait{T}"/>, which is only ever called off the UI thread.
/// </remarks>
internal sealed partial class PyYtClient
{
    /// <summary>Runs a search and returns its first page together with the object that pages the rest.</summary>
    /// <param name="filter">The row the user picked in the search dialog's filter, 0-5; see the mapping below.</param>
    internal (IReadOnlyList<YouTubeResult> Items, SearchPage Page) Search(
        string query, int filter, int count, CancellationToken token)
    {
        int limit = Math.Max(1, count);
        SearchBase search = filter switch
        {
            1 => new CustomSearch(query, SearchMode.Livestreams, limit: limit),
            2 => new CustomSearch(query, VideoSortOrder.UploadDate, limit: limit),
            3 => new CustomSearch(query, VideoSortOrder.ViewCount, limit: limit),
            4 => new PlaylistsSearch(query, limit: limit),
            5 => new ChannelsSearch(query, limit: limit),
            // No filter: videos only, which is what the player played before filters existed.
            _ => new VideosSearch(query, limit: limit),
        };
        var page = new SearchPage(search);
        return (Wait(page.Take(limit, token)), page);
    }

    /// <summary>The title of a playlist and every video in it.</summary>
    internal (string Title, IReadOnlyList<YouTubeResult> Items) Playlist(string url, CancellationToken token)
    {
        using var playlist = new PyYt.Playlist(url);
        // Collect the whole playlist, as the old client did: the results feed shows it in one go.
        while (playlist.HasMoreVideos)
        {
            token.ThrowIfCancellationRequested();
            Wait(playlist.GetNextVideosAsync(token));
        }
        var items = new List<YouTubeResult>();
        foreach (JsonNode? node in playlist.Videos)
            if (node is JsonObject video && ToResult(video) is YouTubeResult row)
                items.Add(row);
        return (Str(playlist.Info, "title") ?? string.Empty, items);
    }

    /// <summary>A channel's title and the playlists it publishes.</summary>
    internal (string Title, IReadOnlyList<YouTubeResult> Items) ChannelPlaylists(
        string channelId, CancellationToken token)
    {
        using var channel = new PyYt.Channel(channelId);
        Wait(channel.InitializeAsync(token));
        while (channel.HasMorePlaylists)
        {
            token.ThrowIfCancellationRequested();
            Wait(channel.NextAsync(token));
        }
        var items = new List<YouTubeResult>();
        if (channel.Result["playlists"] is JsonArray playlists)
            foreach (JsonNode? node in playlists)
                if (node is JsonObject entry && ChannelPlaylistResult(entry) is YouTubeResult row)
                    items.Add(row);
        return (Str(channel.Result, "title") ?? string.Empty, items);
    }

    /// <summary>The metadata of a single video.</summary>
    internal YouTubeResult Video(string url, CancellationToken token)
        => ComponentToResult(Wait(PyYt.Video.GetAsync(url, cancellationToken: token)));

    /// <summary>A video's long description.</summary>
    internal string Description(string url, CancellationToken token)
        => Str(Wait(PyYt.Video.GetAsync(url, cancellationToken: token)), "description") ?? string.Empty;

    /// <summary>The words YouTube offers to finish what the user has typed, for the search box's suggestions.
    /// </summary>
    /// <remarks>
    /// Called from a background task the search dialog starts on each keystroke, never the UI thread, so it
    /// blocks with <see cref="Wait{T}"/> like the rest. An empty list is a perfectly good answer - nothing to
    /// offer - so the caller need not tell "no suggestions" apart from "the query was blank".
    /// </remarks>
    internal IReadOnlyList<string> Suggestions(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();
        JsonObject response = Wait(PyYt.Suggestions.GetAsync(query, cancellationToken: token));
        if (response["result"] is not JsonArray words)
            return Array.Empty<string>();
        var found = new List<string>(words.Count);
        foreach (JsonNode? node in words)
            if (node is JsonValue value && value.TryGetValue(out string? word) && !string.IsNullOrEmpty(word))
                found.Add(word);
        return found;
    }
}

internal sealed partial class PyYtClient
{
    /// <summary>The canonical watch page for a video id.</summary>
    internal static string WatchUrl(string id) => $"https://www.youtube.com/watch?v={id}";

    // A YouTube video id is exactly eleven of these characters and nothing else.
    [GeneratedRegex("^[a-zA-Z0-9_-]{11}$")]
    private static partial Regex VideoIdShape();

    /// <summary>The canonical watch URL for whatever a link points at, or null when it names no video.</summary>
    /// <remarks>
    /// PyYt's id parser returns non-YouTube input verbatim rather than refusing it, so its answer is held to
    /// the strict shape of a real id before it is trusted; anything else - a playlist link, a channel page,
    /// a bare word - yields null, which is the "this is not a video" the callers expect.
    /// </remarks>
    internal static string? Canonical(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return null;
        string id;
        try
        {
            id = PyYt.Video.GetVideoId(link);
        }
        catch
        {
            return null;
        }
        return VideoIdShape().IsMatch(id) ? WatchUrl(id) : null;
    }

    /// <summary>Turns whatever PyYt threw into the neutral outcome the player reports.</summary>
    /// <remarks>
    /// PyYt draws a coarser set of distinctions than the old library did. A cancellation the user asked for
    /// is told apart from every other failure by the token, not the exception type, because a cancellation
    /// can arrive carrying somebody else's token. Everything network-shaped - a refused request, a socket
    /// that dropped, a rate-limit surfacing as an HTTP error - reads as Network; a page PyYt could not make
    /// sense of reads as Unknown. Note there is no RateLimited here: that verdict now comes only from yt-dlp,
    /// which sees the "429" text; a rate limit reaching PyYt looks like any other network failure.
    /// </remarks>
    internal static ResolveOutcome Explain(Exception failure, CancellationToken token) => failure switch
    {
        OperationCanceledException when token.IsCancellationRequested => ResolveOutcome.Cancelled,
        OperationCanceledException => ResolveOutcome.Failed(ResolveFailure.Network, failure.Message),
        VideoNotFoundError => ResolveOutcome.Failed(ResolveFailure.Unavailable, failure.Message),
        RequestError => ResolveOutcome.Failed(ResolveFailure.Network, failure.Message),
        HttpRequestException or IOException => ResolveOutcome.Failed(ResolveFailure.Network, failure.Message),
        ParsingError => ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message),
        PyYtException => ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message),
        _ => ResolveOutcome.Failed(ResolveFailure.Unknown, failure.Message),
    };

    /// <summary>Maps one search-result object to a neutral row, dispatching on the kind PyYt tagged it with.
    /// A video from inside a playlist carries no tag and falls to the video mapping, which is what it is.</summary>
    internal static YouTubeResult? ToResult(JsonObject item) => Str(item, "type") switch
    {
        "playlist" => PlaylistResult(item),
        "channel" => ChannelResult(item),
        _ => VideoResult(item),
    };

    private static YouTubeResult? VideoResult(JsonObject item)
    {
        string? id = Str(item, "id");
        string url = id is not null ? WatchUrl(id) : Str(item, "link") ?? string.Empty;
        if (url.Length == 0)
            return null;
        return new YouTubeResult(
            id ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            Str(item, "channel", "name") ?? string.Empty,
            ParseDuration(Str(item, "duration")),
            url,
            Str(item, "channel", "link") ?? string.Empty)
        {
            Views = Str(item, "viewCount", "text"),
            PublishedTime = Str(item, "publishedTime"),
            ItemType = YouTubeItemType.Video,
        };
    }

    // The metadata endpoint states a video's length as a raw count of seconds under duration.secondsText,
    // where search and playlist rows give a "mm:ss" string; ParseDuration reads both.
    private static YouTubeResult ComponentToResult(JsonObject component)
    {
        string? id = Str(component, "id");
        string url = id is not null ? WatchUrl(id) : Str(component, "link") ?? string.Empty;
        return new YouTubeResult(
            id ?? string.Empty,
            Str(component, "title") ?? string.Empty,
            Str(component, "channel", "name") ?? string.Empty,
            ParseDuration(Str(component, "duration", "secondsText")),
            url,
            Str(component, "channel", "link") ?? string.Empty)
        {
            Views = Str(component, "viewCount", "text"),
            ItemType = YouTubeItemType.Video,
        };
    }

    private static YouTubeResult PlaylistResult(JsonObject item)
    {
        string? id = Str(item, "id");
        string url = Str(item, "link")
            ?? (id is null ? string.Empty : $"https://www.youtube.com/playlist?list={id}");
        return new YouTubeResult(
            id ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            Str(item, "channel", "name") ?? string.Empty,
            null,
            url,
            Str(item, "channel", "link") ?? string.Empty)
        {
            // The count of videos rides in Views: it is the one secondary line a playlist row has to show.
            Views = Str(item, "videoCount"),
            ItemType = YouTubeItemType.Playlist,
        };
    }

    private static YouTubeResult ChannelResult(JsonObject item)
    {
        string? id = Str(item, "id");
        string url = Str(item, "link")
            ?? (id is null ? string.Empty : $"https://www.youtube.com/channel/{id}");
        return new YouTubeResult(
            id ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            Str(item, "title") ?? string.Empty,
            null,
            url,
            url)
        {
            Views = Str(item, "subscribers"),
            PublishedTime = Str(item, "videoCount"),
            ItemType = YouTubeItemType.Channel,
        };
    }

    // A channel's own playlists arrive in a leaner shape than a search hit - an id, a title, a count, when
    // it was last touched, and no link at all - so the watch/list URL is built from the id by hand.
    private static YouTubeResult? ChannelPlaylistResult(JsonObject entry)
    {
        string? id = Str(entry, "id");
        if (id is null)
            return null;
        return new YouTubeResult(
            id,
            Str(entry, "title") ?? string.Empty,
            string.Empty,
            null,
            $"https://www.youtube.com/playlist?list={id}",
            string.Empty)
        {
            Views = Str(entry, "videoCount"),
            PublishedTime = Str(entry, "lastEdited"),
            ItemType = YouTubeItemType.Playlist,
        };
    }

    /// <summary>Reads a string that lives at the end of a path of object keys, or null if any step is missing.
    /// PyYt keeps its own navigator internal, so this is the small piece of it the mappings above need.</summary>
    private static string? Str(JsonNode? node, params string[] keys)
    {
        JsonNode? current = node;
        foreach (string key in keys)
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(key, out current) || current is null)
                return null;
        }
        if (current is JsonValue value && value.TryGetValue(out string? text))
            return text;
        return current?.ToString();
    }

    /// <summary>Reads a duration written either as whole seconds or as "mm:ss"/"h:mm:ss"; null for a live
    /// stream, which states no length.</summary>
    private static TimeSpan? ParseDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim();
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
            return TimeSpan.FromSeconds(seconds);
        string[] parts = text.Split(':');
        if (parts.Length is < 2 or > 3)
            return null;
        try
        {
            int hours = parts.Length == 3 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
            int minutes = int.Parse(parts[^2], CultureInfo.InvariantCulture);
            int secs = int.Parse(parts[^1], CultureInfo.InvariantCulture);
            return new TimeSpan(hours, minutes, secs);
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>Runs an async call to completion on the calling thread. Only ever called from a background
    /// job, never the UI thread, so the block it does is a block on a thread that is meant to wait.</summary>
    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private static void Wait(Task task) => task.GetAwaiter().GetResult();
}
