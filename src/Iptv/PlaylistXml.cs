using System.Xml;

namespace LunaPlayer.Iptv;

/// <summary>The one <see cref="XmlReaderSettings"/> the XML playlist readers share.</summary>
///
/// <remarks>
/// Playlists arrive from the network and from files the user did not write, so the reader is locked down the
/// way any parser fed untrusted input should be: no DTD processing at all (which closes the door on external
/// entity and billion-laughs attacks) and no resolver, so nothing a document names is ever fetched. Whitespace,
/// comments and processing instructions are dropped because no playlist reader looks at them.
/// </remarks>
internal static class PlaylistXml
{
    internal static XmlReaderSettings Settings { get; } = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
    };
}
