using System.Xml;
using LunaPlayer.Media;

namespace LunaPlayer.Iptv;

/// <summary>Reads an ASX playlist, the XML-shaped format Windows Media Player uses.</summary>
///
/// <remarks>
/// ASX is XML in spirit only: its tags are case-insensitive (<c>&lt;ASX&gt;</c>, <c>&lt;Ref&gt;</c>,
/// <c>&lt;ENTRY&gt;</c> all appear), and real files are routinely not well-formed - unescaped ampersands in
/// URLs, a missing root close, stray markup. So it is read twice over: first with a streaming
/// <see cref="XmlReader"/> for the well-behaved majority, and, when that throws, with a forgiving hand scan
/// that pulls <c>href</c> values out of <c>&lt;ref&gt;</c> tags and titles out of <c>&lt;title&gt;</c> tags
/// without demanding valid XML. A playable location comes from a <c>&lt;ref href&gt;</c>; the nearest preceding
/// <c>&lt;title&gt;</c> names it.
/// </remarks>
internal static class AsxPlaylist
{
    internal static M3uPlaylistResult Parse(string text, Uri source, bool local)
    {
        var entries = ParseWithReader(text, source, local);
        // A malformed file returns null from the strict reader; the hand scan is the fallback for it.
        entries ??= ParseByHand(text, source, local);
        return new(entries, IsHls: false, null);
    }

    private static List<M3uEntry>? ParseWithReader(string text, Uri source, bool local)
    {
        var entries = new List<M3uEntry>();
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), PlaylistXml.Settings);
            string? title = null;
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    var name = reader.LocalName.ToLowerInvariant();
                    if (name == "title")
                    {
                        title = reader.ReadElementContentAsString().Trim();
                        continue;
                    }
                    if (name == "ref")
                    {
                        var href = Attribute(reader, "href");
                        if (href is { Length: > 0 })
                        {
                            var resolved = M3uPlaylist.Resolve(href, source, local);
                            if (resolved is not null)
                                entries.Add(new(resolved, string.IsNullOrWhiteSpace(title) ? null : title));
                        }
                        title = null;
                    }
                }
                reader.Read();
            }
            return entries;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>The value of the named attribute on the current element, matched without regard to case, or
    /// null when the element does not carry it.</summary>
    private static string? Attribute(XmlReader reader, string name)
    {
        if (!reader.HasAttributes)
            return null;
        for (var index = 0; index < reader.AttributeCount; index++)
        {
            reader.MoveToAttribute(index);
            if (reader.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var value = reader.Value.Trim();
                reader.MoveToElement();
                return value;
            }
        }
        reader.MoveToElement();
        return null;
    }

    /// <summary>The forgiving fallback: walks the text for <c>&lt;ref ... href="..."&gt;</c> tags, taking the
    /// most recent <c>&lt;title&gt;...&lt;/title&gt;</c> before each as its name. Case-insensitive and
    /// indifferent to whether the surrounding document is valid XML.</summary>
    private static List<M3uEntry> ParseByHand(string text, Uri source, bool local)
    {
        var entries = new List<M3uEntry>();
        string? title = null;
        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf('<', position);
            if (open < 0)
                break;
            var close = text.IndexOf('>', open + 1);
            if (close < 0)
                break;
            var tag = text[(open + 1)..close];
            position = close + 1;

            var trimmed = tag.TrimStart('/').TrimStart();
            if (trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase) && !tag.StartsWith("/"))
            {
                var end = text.IndexOf('<', position);
                title = (end < 0 ? text[position..] : text[position..end]).Trim();
                if (title.Length == 0)
                    title = null;
            }
            else if (trimmed.StartsWith("ref", StringComparison.OrdinalIgnoreCase))
            {
                var href = HrefFromTag(tag);
                if (href is { Length: > 0 })
                {
                    var resolved = M3uPlaylist.Resolve(href, source, local);
                    if (resolved is not null)
                        entries.Add(new(resolved, title));
                }
                title = null;
            }
        }
        return entries;
    }

    /// <summary>Pulls the <c>href</c> value out of a raw <c>ref</c> tag's text, quoted with either kind of
    /// quote, or null when there is none.</summary>
    private static string? HrefFromTag(string tag)
    {
        var at = tag.IndexOf("href", StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return null;
        var equals = tag.IndexOf('=', at);
        if (equals < 0)
            return null;
        var rest = tag[(equals + 1)..].TrimStart();
        if (rest.Length == 0)
            return null;
        var quote = rest[0];
        if (quote is '"' or '\'')
        {
            var end = rest.IndexOf(quote, 1);
            return end < 0 ? null : rest[1..end].Trim();
        }
        var space = rest.IndexOfAny([' ', '\t', '/', '>']);
        return (space < 0 ? rest : rest[..space]).Trim();
    }
}
