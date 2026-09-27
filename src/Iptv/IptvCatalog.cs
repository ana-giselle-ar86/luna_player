using LunaPlayer.Media;

namespace LunaPlayer.Iptv;

/// <summary>The channels and categories loaded from one IPTV source, in the single shape the channel browser
/// and the player work in whatever the source was.</summary>
///
/// <remarks>
/// An extended M3U list, an Xtream account and a Stalker portal all reduce to this: a flat list of
/// <see cref="IptvChannel"/> and the <see cref="IptvCategory"/> groups they fall into. <see cref="TvgUrl"/> is
/// the programme-guide address the source advertised, used to load EPG when the source carries no explicit one.
/// </remarks>
internal sealed record IptvCatalog(
    IReadOnlyList<IptvCategory> Categories,
    IReadOnlyList<IptvChannel> Channels,
    string? TvgUrl)
{
    internal static IptvCatalog Empty { get; } = new([], [], null);

    /// <summary>The programme guide loaded for this catalog, or null when the source advertised none, carried
    /// no explicit EPG address, or its guide could not be loaded. Attached after the channels themselves are
    /// built, since loading it reaches the network and is best-effort.</summary>
    internal EpgGuide? Guide { get; init; }

    /// <summary>Turns a parsed extended M3U playlist into a catalog, dividing its entries into categories by
    /// their <c>group-title</c>. Entries with no group fall into one shared "Uncategorized" bucket rather than
    /// each becoming its own group, which a flat provider list without groups would otherwise produce.</summary>
    /// <param name="uncategorized">The name to give the group that holds entries carrying no group of their
    /// own. Worded by the caller on the UI thread and passed in, because this runs on a worker thread.</param>
    internal static IptvCatalog FromM3u(M3uPlaylistResult result, string uncategorized)
    {
        var channels = new List<IptvChannel>(result.Entries.Count);
        // A category is keyed by its group name; the first spelling seen wins its display name. Ordinal so
        // "News" and "news" stay apart only if the provider itself keeps them apart.
        var categories = new List<IptvCategory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var entry in result.Entries)
        {
            var attributes = entry.Attributes ?? default;
            var group = (attributes.GroupTitle ?? string.Empty).Trim();
            var categoryId = group.Length == 0 ? string.Empty : group;
            if (seen.Add(categoryId))
                categories.Add(new IptvCategory(categoryId, group.Length == 0 ? uncategorized : group, StreamKind.Live));
            var name = FirstNonEmpty(entry.Title, attributes.TvgName) ?? entry.Location;
            channels.Add(new IptvChannel(
                Id: index.ToString(),
                Name: name,
                Number: Trimmed(attributes.TvgChno),
                CategoryId: categoryId,
                Kind: StreamKind.Live,
                EpgId: Trimmed(attributes.TvgId),
                Logo: Trimmed(attributes.TvgLogo),
                Url: entry.Location,
                StalkerCmd: null));
            index++;
        }
        return new IptvCatalog(categories, channels, Trimmed(result.TvgUrl));
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first)) return first.Trim();
        if (!string.IsNullOrWhiteSpace(second)) return second.Trim();
        return null;
    }

    private static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
