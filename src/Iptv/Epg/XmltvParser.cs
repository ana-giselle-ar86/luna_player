using System.IO.Compression;
using System.Net;
using System.Xml;

namespace LunaPlayer.Iptv;

/// <summary>Turns an XMLTV feed into an <see cref="EpgGuide"/>: fetches it, unzips it when it arrives zipped,
/// and reads it as a stream so a hundred-megabyte guide never has to be held in memory at once.</summary>
///
/// <remarks>
/// Best-effort by design. A guide is a nicety layered over channels that already play, so every failure here -
/// a refusal, a timeout, malformed XML, a body too large - yields no guide rather than an error: the caller
/// gets <c>null</c> and the browser simply shows no "Now" column. It runs on a worker thread behind the same
/// progress window as the channel load.
///
/// XMLTV is read with <see cref="XmlReader"/>, never <c>XDocument</c>, because these files reach 100+ MB and
/// loading one into a tree would exhaust memory. Feeds are commonly served as <c>.xml.gz</c> with no
/// <c>Content-Encoding</c> header, so gzip is detected by the file's own magic bytes rather than trusted to
/// the transport.
/// </remarks>
internal static class XmltvParser
{
    // A guide larger than this is treated as misbehaving rather than read without end. Generous - a year of
    // programmes for thousands of channels still fits - but bounded.
    private const long MaximumBytes = 512L * 1024 * 1024;

    // More programmes than this and we stop reading; bounds memory against a feed that never ends.
    private const int MaximumProgrammes = 4_000_000;

    private static readonly HttpClient Client = CreateClient();

    /// <summary>Loads and parses the guide at the given url, or null when anything at all went wrong. Never
    /// throws for a network or parsing fault; only a cancellation the caller asked for propagates.</summary>
    internal static EpgGuide? TryLoad(string url, CancellationToken token)
    {
        if (!Media.LinkValidator.TryGetHttpUrl(url.Trim(), out var uri))
            return null;
        try
        {
            using var response = Client
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token)
                .GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaximumBytes)
                return null;

            using var network = response.Content.ReadAsStream(token);
            using var bounded = new BoundedStream(network, MaximumBytes);
            using var decoded = Decompress(bounded);
            return Parse(decoded, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException
            or OperationCanceledException or XmlException or InvalidDataException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>Wraps the stream in a <see cref="GZipStream"/> when its first two bytes are the gzip magic
    /// number, leaving it untouched otherwise. The two peeked bytes are put back so the reader sees them.</summary>
    private static Stream Decompress(Stream source)
    {
        var header = new byte[2];
        var read = 0;
        while (read < 2)
        {
            var got = source.Read(header, read, 2 - read);
            if (got == 0)
                break;
            read += got;
        }
        var head = new HeadStream(header, read, source);
        return read == 2 && header[0] == 0x1F && header[1] == 0x8B
            ? new GZipStream(head, CompressionMode.Decompress)
            : head;
    }

    private static EpgGuide Parse(Stream stream, CancellationToken token)
    {
        var byChannel = new Dictionary<string, List<EpgProgramme>>(StringComparer.OrdinalIgnoreCase);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        };
        using var reader = XmlReader.Create(stream, settings);
        var count = 0;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "programme")
                continue;
            token.ThrowIfCancellationRequested();
            if (ReadProgramme(reader) is not EpgProgramme programme)
                continue;
            if (!byChannel.TryGetValue(programme.ChannelId, out var list))
                byChannel[programme.ChannelId] = list = [];
            list.Add(programme);
            if (++count >= MaximumProgrammes)
                break;
        }

        foreach (var list in byChannel.Values)
            list.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return new EpgGuide(byChannel);
    }

    /// <summary>Reads one <c>&lt;programme&gt;</c> element and its title/description children, positioned on the
    /// start tag. Null when it carries no channel, no usable times, or no title.</summary>
    private static EpgProgramme? ReadProgramme(XmlReader reader)
    {
        var channel = (reader.GetAttribute("channel") ?? string.Empty).Trim();
        var start = ParseTime(reader.GetAttribute("start"));
        var stop = ParseTime(reader.GetAttribute("stop"));
        if (channel.Length == 0 || start is null || stop is null)
        {
            reader.Skip();
            return null;
        }

        string? title = null;
        string? description = null;
        if (!reader.IsEmptyElement)
        {
            var depth = reader.Depth;
            while (reader.Read() && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;
                switch (reader.Name)
                {
                    case "title" when title is null:
                        title = reader.ReadElementContentAsString().Trim();
                        break;
                    case "desc" when description is null:
                        description = reader.ReadElementContentAsString().Trim();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
        }

        return title is { Length: > 0 }
            ? new EpgProgramme(channel, start.Value, stop.Value, title, description is { Length: > 0 } ? description : null)
            : null;
    }

    /// <summary>Parses an XMLTV timestamp - <c>YYYYMMDDHHMMSS</c> optionally followed by a <c>+HHMM</c> offset,
    /// and tolerant of shorter forms that stop at the minute or the day. Null when it is not a time at all.</summary>
    private static DateTimeOffset? ParseTime(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length < 8)
            return null;

        var offset = TimeSpan.Zero;
        var space = text.IndexOf(' ');
        if (space > 0)
        {
            var zone = text[(space + 1)..].Trim();
            if (!TryParseOffset(zone, out offset))
                offset = TimeSpan.Zero;
            text = text[..space];
        }

        if (!TryDigits(text, 0, 4, out var year) || !TryDigits(text, 4, 2, out var month)
            || !TryDigits(text, 6, 2, out var day))
            return null;
        TryDigits(text, 8, 2, out var hour);
        TryDigits(text, 10, 2, out var minute);
        TryDigits(text, 12, 2, out var second);

        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Parses a <c>+HHMM</c> / <c>-HHMM</c> timezone offset. False when it is not one.</summary>
    private static bool TryParseOffset(string zone, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (zone.Length < 5 || (zone[0] != '+' && zone[0] != '-'))
            return false;
        if (!TryDigits(zone, 1, 2, out var hours) || !TryDigits(zone, 3, 2, out var minutes))
            return false;
        offset = new TimeSpan(hours, minutes, 0);
        if (zone[0] == '-')
            offset = -offset;
        return true;
    }

    private static bool TryDigits(string text, int start, int length, out int value)
    {
        value = 0;
        if (start + length > text.Length)
            return false;
        for (var i = start; i < start + length; i++)
        {
            if (text[i] is < '0' or > '9')
                return false;
            value = value * 10 + (text[i] - '0');
        }
        return true;
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
    }

    /// <summary>A read-only stream that yields a handful of already-peeked bytes before handing off to the
    /// stream they were read from, so a reader that needed to look at the header still sees the whole body.</summary>
    private sealed class HeadStream(byte[] head, int headLength, Stream rest) : Stream
    {
        private int _offset;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_offset < headLength)
            {
                var take = Math.Min(count, headLength - _offset);
                Array.Copy(head, _offset, buffer, offset, take);
                _offset += take;
                return take;
            }
            return rest.Read(buffer, offset, count);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                rest.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>A read-only stream that gives up once more than a set number of bytes have passed through it,
    /// so a feed that claims no length yet never ends is still bounded.</summary>
    private sealed class BoundedStream(Stream inner, long limit) : Stream
    {
        private long _read;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var got = inner.Read(buffer, offset, count);
            _read += got;
            if (_read > limit)
                throw new InvalidDataException("EPG feed exceeded the maximum allowed size.");
            return got;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
