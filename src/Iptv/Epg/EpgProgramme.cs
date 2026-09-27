namespace LunaPlayer.Iptv;

/// <summary>One entry in the programme guide: what is on one channel over one span of time.</summary>
/// <param name="ChannelId">The XMLTV channel id this belongs to, matched against a channel's <c>tvg-id</c>.</param>
/// <param name="Start">When the programme begins.</param>
/// <param name="Stop">When it ends.</param>
/// <param name="Title">Its name.</param>
/// <param name="Description">A longer description, when the guide carried one.</param>
internal readonly record struct EpgProgramme(
    string ChannelId,
    DateTimeOffset Start,
    DateTimeOffset Stop,
    string Title,
    string? Description);
