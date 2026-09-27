using System.Xml;
using LunaPlayer.Media;

namespace LunaPlayer.Iptv;

/// <summary>Reads an XSPF ("spiff") playlist, the XML format used by VLC and other open players.</summary>
///
/// <remarks>
/// Parsed with a streaming <see cref="XmlReader"/> rather than a document model: a playlist can be large, and
/// there is never a reason to hold the whole tree. Only what the player can use is read - each
/// <c>&lt;track&gt;</c>'s <c>&lt;location&gt;</c> and <c>&lt;title&gt;</c> - and everything else is stepped
/// over. Element names are matched by local name so a namespaced document reads the same as a bare one.
///
/// <see cref="XmlReader.ReadElementContentAsString"/> already leaves the reader on the node after the element,
/// so those branches must not advance again; the loop only calls <see cref="XmlReader.Read"/> where it did not
/// consume the element itself.
/// </remarks>
internal static class XspfPlaylist
{
    internal static M3uPlaylistResult Parse(string text, Uri source, bool local)
    {
        var entries = new List<M3uEntry>();
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), PlaylistXml.Settings);
            string? location = null;
            string? title = null;
            var inTrack = false;
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName.ToLowerInvariant())
                    {
                        case "track":
                            inTrack = true;
                            location = null;
                            title = null;
                            reader.Read();
                            continue;
                        case "location" when inTrack:
                            location = reader.ReadElementContentAsString().Trim();
                            continue;
                        case "title" when inTrack:
                            title = reader.ReadElementContentAsString().Trim();
                            continue;
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement
                    && reader.LocalName.Equals("track", StringComparison.OrdinalIgnoreCase))
                {
                    inTrack = false;
                    if (location is { Length: > 0 })
                    {
                        var resolved = M3uPlaylist.Resolve(location, source, local);
                        if (resolved is not null)
                            entries.Add(new(resolved, string.IsNullOrWhiteSpace(title) ? null : title));
                    }
                }
                reader.Read();
            }
        }
        catch (XmlException exception)
        {
            return M3uPlaylistResult.Failure(exception.Message);
        }
        return new(entries, IsHls: false, null);
    }
}
