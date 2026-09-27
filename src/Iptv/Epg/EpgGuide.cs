namespace LunaPlayer.Iptv;

/// <summary>The programme guide for a whole source: every channel's schedule, keyed by XMLTV channel id, ready
/// to answer "what is on now, and what is next" for any channel.</summary>
///
/// <remarks>
/// Built once, on a worker thread, from a parsed XMLTV feed, then read from the UI thread while the browser is
/// open. It never changes after construction, so it needs no locking. Ids are matched case-insensitively
/// because a channel's <c>tvg-id</c> and the guide's <c>channel id</c> come from different files and rarely
/// agree on case. Each channel's programmes are sorted by start time so now/next is a walk, not a scan.
/// </remarks>
internal sealed class EpgGuide
{
    /// <summary>A guide holding nothing, used when there was no EPG url or it could not be loaded.</summary>
    internal static EpgGuide Empty { get; } = new(new Dictionary<string, List<EpgProgramme>>());

    private readonly Dictionary<string, List<EpgProgramme>> _byChannel;

    internal EpgGuide(Dictionary<string, List<EpgProgramme>> byChannel) => _byChannel = byChannel;

    /// <summary>Whether the guide holds any programmes at all, so callers can skip the "Now" column entirely
    /// rather than looking every row up against an empty table.</summary>
    internal bool IsEmpty => _byChannel.Count == 0;

    /// <summary>What is on the given channel at <paramref name="at"/>, and what follows it. Either may be null:
    /// there may be nothing on now (a gap in the schedule) yet something later, or nothing left at all.</summary>
    internal (EpgProgramme? Now, EpgProgramme? Next) NowNext(string? epgId, DateTimeOffset at)
    {
        if (epgId is not { Length: > 0 } || !_byChannel.TryGetValue(epgId.Trim(), out var programmes))
            return (null, null);

        EpgProgramme? now = null;
        EpgProgramme? next = null;
        foreach (var programme in programmes)
        {
            if (programme.Start <= at && at < programme.Stop)
                now = programme;
            else if (programme.Start > at)
            {
                next = programme;
                break;
            }
        }
        return (now, next);
    }
}
