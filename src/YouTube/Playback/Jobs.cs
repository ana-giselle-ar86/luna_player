using LunaPlayer.Accessibility;
using LunaPlayer.Application;
using LunaPlayer.Configuration;
using LunaPlayer.Configuration.Models;
using LunaPlayer.Favorites;
using LunaPlayer.Media;
using LunaPlayer.Playback;
using LunaPlayer.UI;

namespace LunaPlayer.YouTube.Playback;

internal sealed partial class Sessions
{
    // ---- the background jobs ----

    private SearchResults RunSearch(
        string query,
        int filter,
        int count,
        int videoQuality,
        int audioQuality,
        string fetching,
        Action<ProgressUpdate> report,
        CancellationToken token)
    {
        try
        {
            var (items, page) = _client.Search(query, filter, count, token);
            if (items.Count == 0)
                return new SearchResults([], null, ResolveFailure.None, string.Empty);
            // Only a video is worth resolving ahead: a playlist or channel row opens a browse dialog, not a
            // stream, so a search filtered to those has nothing here to make ready.
            if (items[0].ItemType is YouTubeItemType.Video)
            {
                report(new ProgressUpdate(0, 0, fetching));
                // The sound is set going alongside, so Ctrl+Enter on the first row is ready too, while the
                // picture is waited for: the list opens on its first row played as video, and the point of
                // doing this before the window appears is that choosing that row plays at once. A video that
                // will not resolve is not an error - the user still gets their results - so the outcome is
                // dropped.
                _cache.Prefetch(items[0].Url, items[0], true, audioQuality, token);
                _ = _cache.Wait(items[0].Url, items[0], false, videoQuality, token, token);
            }
            return new SearchResults(items, page, ResolveFailure.None, string.Empty);
        }
        catch (Exception failure)
        {
            var explained = FailureMapping.Explain(failure, token);
            return new SearchResults([], null, explained.Failure, explained.Detail);
        }
    }

    private PlaylistResults RunPlaylist(string link, CancellationToken token)
    {
        var (title, items, failure, detail) = _backend.Playlist(link, token);
        return new PlaylistResults(title, items, failure, detail);
    }

    private void ShowError(string message) =>
        // Translators: Title of the messages the player shows about YouTube.
        _view.Dialogs.ShowError(message, Tr("YouTube"));

    private readonly record struct SearchResults(
        IReadOnlyList<YouTubeResult> Items, Metadata.Page? Page, ResolveFailure Failure, string Detail);

    private readonly record struct PlaylistResults(
        string Title, IReadOnlyList<YouTubeResult> Items, ResolveFailure Failure, string Detail);
}
