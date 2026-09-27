using LunaPlayer.Media;

namespace LunaPlayer.Iptv;

/// <summary>Reads a CUE sheet, taking each <c>FILE</c> it names as one playable entry.</summary>
///
/// <remarks>
/// A CUE sheet describes where the tracks fall inside one or more audio files. The player's entry shape holds
/// a location and a name but not a track's offset within a file, so a cue is read at the level it can act on:
/// each <c>FILE</c> becomes an entry, resolved against the cue's own folder, and mpv plays the file whole. The
/// disc <c>TITLE</c> (or, lacking one, the file name) names it. Reading the internal <c>TRACK</c>/<c>INDEX</c>
/// offsets would need a richer entry than the rest of the player uses, so it is left for later.
/// </remarks>
internal static class CuePlaylist
{
    internal static M3uPlaylistResult Parse(string text, Uri source, bool local)
    {
        var entries = new List<M3uEntry>();
        string? albumTitle = null;
        var seenFile = false;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is string rawLine)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase) && !seenFile)
            {
                // Only the disc-level title, before any FILE; a per-track TITLE cannot name a whole file.
                albumTitle = Unquote(line["TITLE".Length..]);
            }
            else if (line.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
            {
                seenFile = true;
                var name = Unquote(line["FILE".Length..]);
                if (name.Length == 0)
                    continue;
                var location = M3uPlaylist.Resolve(name, source, local);
                if (location is null)
                    continue;
                var title = albumTitle is { Length: > 0 } ? albumTitle : null;
                entries.Add(new(location, title));
            }
        }
        return new(entries, IsHls: false, null);
    }

    /// <summary>The first quoted run in <paramref name="value"/>, or the whole thing trimmed when it carries no
    /// quotes. A CUE's <c>FILE</c> line ends with a format word (<c>WAVE</c>, <c>MP3</c>) that the quotes fence
    /// off; an unquoted name is taken up to the first space for the same reason.</summary>
    private static string Unquote(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
            return string.Empty;
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end < 0 ? text[1..].Trim() : text[1..end];
        }
        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }
}
