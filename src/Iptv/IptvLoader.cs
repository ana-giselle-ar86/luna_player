using LunaPlayer.Media;

namespace LunaPlayer.Iptv;

/// <summary>What loading a source produced: a catalog of channels, or a reason it could not be loaded.</summary>
///
/// <remarks>
/// <see cref="Error"/> is null on success. When it is set it is already worded for the user - the M3U and
/// network readers word their own size and timeout failures, on whichever thread they run. <see cref="Unsupported"/>
/// is the one case with no wording of its own: a source of a kind whose client has not been built yet, which
/// the caller words on the UI thread rather than the worker thread guessing at it.
/// </remarks>
internal sealed record IptvLoadResult(IptvCatalog? Catalog, string? Error)
{
    internal static IptvLoadResult Ok(IptvCatalog catalog) => new(catalog, null);
    internal static IptvLoadResult Fail(string error) => new(null, error);

    /// <summary>A source of a kind not yet supported. Carries no wording; the caller supplies it.</summary>
    internal static IptvLoadResult Unsupported { get; } = new(null, null);
}

/// <summary>Turns a saved source into the channels it holds, whatever kind of source it is.</summary>
///
/// <remarks>
/// This is the single door channel loading goes through, mirroring how <see cref="PlaylistReader"/> is the
/// single door playlist reading goes through. It runs on a worker thread behind a progress window, so it
/// takes the one piece of wording it needs - the name of the catch-all category - rather than reaching for
/// <c>Tr</c> off the UI thread. The Xtream and Stalker kinds are filled in as their clients are built; until
/// then they answer <see cref="IptvLoadResult.Unsupported"/> so the browser refuses cleanly rather than
/// loading nothing.
/// </remarks>
internal static class IptvLoader
{
    /// <param name="uncategorized">The name for the group holding channels that carry none of their own,
    /// chosen on the UI thread and passed in because this runs on a worker thread.</param>
    internal static IptvLoadResult Load(IptvSource source, string uncategorized, CancellationToken cancellationToken)
    {
        var result = source.Kind switch
        {
            IptvSourceKind.M3uUrl => FromM3u(PlaylistReader.ReadNetwork(source.Url, cancellationToken), uncategorized),
            IptvSourceKind.M3uFile => FromM3u(PlaylistReader.ReadLocal(source.Url), uncategorized),
            IptvSourceKind.Xtream => XtreamClient.Load(source, uncategorized, cancellationToken),
            IptvSourceKind.Stalker => StalkerClient.Load(source, uncategorized, cancellationToken),
            _ => IptvLoadResult.Unsupported,
        };
        return result.Catalog is IptvCatalog catalog
            ? IptvLoadResult.Ok(catalog with { Guide = LoadGuide(source, catalog, cancellationToken) })
            : result;
    }

    /// <summary>Loads the programme guide for a catalog, preferring the source's own explicit EPG address over
    /// whatever the playlist advertised. Best-effort: null when there is no address to try or the load failed,
    /// so a guide never stands between the user and their channels.</summary>
    private static EpgGuide? LoadGuide(IptvSource source, IptvCatalog catalog, CancellationToken cancellationToken)
    {
        var url = FirstNonEmpty(source.EpgUrl, catalog.TvgUrl);
        return url is null ? null : XmltvParser.TryLoad(url, cancellationToken);
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first)) return first.Trim();
        if (!string.IsNullOrWhiteSpace(second)) return second.Trim();
        return null;
    }

    private static IptvLoadResult FromM3u(M3uPlaylistResult result, string uncategorized)
        => result.Error is { Length: > 0 } error
            ? IptvLoadResult.Fail(error)
            : IptvLoadResult.Ok(IptvCatalog.FromM3u(result, uncategorized));
}
