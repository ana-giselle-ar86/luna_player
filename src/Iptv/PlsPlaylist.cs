using LunaPlayer.Media;

namespace LunaPlayer.Iptv;

/// <summary>Reads a PLS playlist, the INI-style format Winamp and SHOUTcast use.</summary>
///
/// <remarks>
/// A PLS file is a single <c>[playlist]</c> section of numbered keys: <c>File1=</c>, <c>Title1=</c>,
/// <c>File2=</c> and so on, in no guaranteed order. Entries are gathered by their number so a title finds its
/// file however the two are interleaved, and emitted in numeric order. The result is the same
/// <see cref="M3uPlaylistResult"/> the M3U reader returns, so the rest of the player treats every playlist the
/// same way.
/// </remarks>
internal static class PlsPlaylist
{
    internal static M3uPlaylistResult Parse(string text, Uri source, bool local)
    {
        var files = new Dictionary<int, string>();
        var titles = new Dictionary<int, string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is string rawLine)
        {
            var line = rawLine.Trim();
            var equals = line.IndexOf('=');
            if (equals <= 0)
                continue;
            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (value.Length == 0)
                continue;
            if (TryNumbered(key, "File", out var fileNumber))
                files[fileNumber] = value;
            else if (TryNumbered(key, "Title", out var titleNumber))
                titles[titleNumber] = value;
        }

        var entries = new List<M3uEntry>();
        foreach (var number in files.Keys.OrderBy(number => number))
        {
            var location = M3uPlaylist.Resolve(files[number], source, local);
            if (location is null)
                continue;
            var title = titles.TryGetValue(number, out var found) && found.Length > 0 ? found : null;
            entries.Add(new(location, title));
        }
        return new(entries, IsHls: false, null);
    }

    /// <summary>Whether <paramref name="key"/> is <paramref name="prefix"/> followed by a number, and what
    /// that number is. <c>File12</c> under prefix <c>File</c> yields 12.</summary>
    private static bool TryNumbered(string key, string prefix, out int number)
    {
        number = 0;
        return key.Length > prefix.Length
            && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key.AsSpan(prefix.Length), out number);
    }
}
