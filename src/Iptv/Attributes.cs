namespace LunaPlayer.Iptv;

/// <summary>The extra facts an IPTV playlist attaches to a channel beyond its address and display name.</summary>
///
/// <remarks>
/// These are the attributes carried on an extended M3U <c>#EXTINF</c> line, written as <c>key="value"</c>
/// pairs before the comma. None of them is required and real provider lists leave most of them out, so every
/// field is optional. They are read for grouping (<see cref="GroupTitle"/>), for a nicer name than the one
/// after the comma (<see cref="TvgName"/>), and for tying a channel to its programme guide
/// (<see cref="TvgId"/>).
/// </remarks>
/// <param name="TvgId">The channel's guide id, matched against an XMLTV <c>channel id</c>.</param>
/// <param name="TvgName">The channel's proper name, often cleaner than the text after the comma.</param>
/// <param name="TvgLogo">A web address for the channel's logo, kept but not displayed by this player.</param>
/// <param name="GroupTitle">The group the channel belongs to, used to divide a flat list into categories.</param>
/// <param name="TvgChno">The channel number the provider assigns, shown beside the name when present.</param>
/// <param name="Catchup">The catch-up scheme the channel supports, kept for a future replay feature.</param>
internal readonly record struct IptvAttributes(
    string? TvgId,
    string? TvgName,
    string? TvgLogo,
    string? GroupTitle,
    string? TvgChno,
    string? Catchup)
{
    /// <summary>Whether anything at all was supplied. A flat M3U with no attributes yields entries whose
    /// attributes are all null, and the reader keeps that as "no attributes" rather than an empty record.</summary>
    internal bool IsEmpty =>
        TvgId is null && TvgName is null && TvgLogo is null
        && GroupTitle is null && TvgChno is null && Catchup is null;
}
