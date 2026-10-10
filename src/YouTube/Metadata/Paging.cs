using System.Text.Json.Nodes;
using PyYt;

namespace LunaPlayer.YouTube.Metadata;

/// <summary>A page of search results that pages forward on demand.</summary>
///
/// <remarks>
/// PyYt's search objects are stateful: each call to <see cref="SearchBase.NextAsync"/> returns the next
/// batch and re-derives the continuation token, and <see cref="SearchBase.HasMoreResults"/> only tells the
/// truth after the first call has run. This wrapper keeps the object alive across the pages Luna's results
/// feed asks for, hands back neutral <see cref="YouTubeResult"/> rows, and serialises the calls behind a
/// gate because the feed can ask for more before the previous ask has returned.
/// </remarks>
internal sealed class Page : IResultPage, IAsyncDisposable
{
    private readonly SearchBase _search;
    private readonly SemaphoreSlim _turn = new(1, 1);
    // False until the first NextAsync has run, because HasMoreResults cannot be trusted before then: a
    // brand-new search has no continuation and would otherwise look exhausted before it had fetched a thing.
    private bool _started;
    private bool _exhausted;

    internal Page(SearchBase search) => _search = search;

    public bool HasMore => !_exhausted;

    public async Task<IReadOnlyList<YouTubeResult>> Take(int count, CancellationToken token)
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
                        if (node is JsonObject item && Mapping.ToResult(item) is YouTubeResult row)
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
