using System.Text.Json.Serialization;

namespace LunaPlayer.Iptv;

/// <summary>How a saved IPTV source is reached, which decides how its channels are loaded.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IptvSourceKind>))]
internal enum IptvSourceKind
{
    /// <summary>An extended M3U/M3U8 playlist downloaded from a web address.</summary>
    [JsonStringEnumMemberName("m3u_url")] M3uUrl,

    /// <summary>An extended M3U/M3U8 playlist read from a file on this computer.</summary>
    [JsonStringEnumMemberName("m3u_file")] M3uFile,

    /// <summary>A provider portal spoken to through the Xtream Codes API (server, username, password).</summary>
    [JsonStringEnumMemberName("xtream")] Xtream,

    /// <summary>A set-top-box portal spoken to through the Stalker protocol (portal URL and a MAC address).</summary>
    [JsonStringEnumMemberName("stalker")] Stalker,
}

/// <summary>What a channel plays: a live broadcast, a film, or an episode of a series.</summary>
/// <remarks>
/// Extended M3U lists do not distinguish these - every entry is treated as <see cref="Live"/> - but the
/// Xtream and Stalker APIs return them apart, and the channel browser lets the user filter on them.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StreamKind>))]
internal enum StreamKind
{
    [JsonStringEnumMemberName("live")] Live,
    [JsonStringEnumMemberName("movie")] Movie,
    [JsonStringEnumMemberName("series")] Series,
}

/// <summary>One IPTV source the user has saved to come back to.</summary>
///
/// <remarks>
/// A single class holds the fields of every kind because the kinds overlap and a user changing a source from
/// one kind to another should not lose what they typed. Which fields matter is decided by <see cref="Kind"/>:
/// an <see cref="IptvSourceKind.M3uUrl"/> uses <see cref="Url"/> alone, an <see cref="IptvSourceKind.Xtream"/>
/// uses <see cref="Url"/> (the server), <see cref="Username"/> and <see cref="Password"/>, and so on.
///
/// The credential fields are written to disk. They are held here in the clear; whether they are encrypted at
/// rest is the store's concern, not this record's.
/// </remarks>
internal sealed class IptvSource
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public IptvSourceKind Kind { get; set; }

    /// <summary>The web address, file path, Xtream server, or Stalker portal, depending on <see cref="Kind"/>.</summary>
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;

    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;

    /// <summary>The MAC address a Stalker portal is bound to, as <c>00:1A:79:...</c>.</summary>
    [JsonPropertyName("mac")] public string MacAddress { get; set; } = string.Empty;

    /// <summary>An optional device serial some Stalker portals also check.</summary>
    [JsonPropertyName("serial")] public string Serial { get; set; } = string.Empty;

    /// <summary>An optional XMLTV programme-guide address, used when the source does not carry its own.</summary>
    [JsonPropertyName("epgUrl")] public string EpgUrl { get; set; } = string.Empty;

    /// <summary>Whether live channels are opened as HLS (<c>.m3u8</c>) rather than transport streams
    /// (<c>.ts</c>). Off by default because a raw transport stream starts faster.</summary>
    [JsonPropertyName("preferHls")] public bool PreferHls { get; set; }

    /// <summary>When it was saved, in seconds since the Unix epoch, matching the favourites file's shape.</summary>
    [JsonPropertyName("created")] public double Created { get; set; }
}

/// <summary>The file holding every saved IPTV source.</summary>
internal sealed class IptvSourceDocument
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("items")] public List<IptvSource> Items { get; set; } = [];
}

/// <summary>What the user typed into the add-or-edit form, before it is checked and turned into a saved
/// <see cref="IptvSource"/>.</summary>
///
/// <remarks>
/// A draft carries no id and no created time: those belong to a saved source and are assigned by the store.
/// It never crosses JSON - only <see cref="IptvSource"/> does - so it needs no serializer registration. The
/// fields untrimmed here are trimmed by <see cref="ToSource"/>, with the sole exception of the password, which
/// is stored exactly as typed because a space can be part of it.
/// </remarks>
internal sealed record IptvSourceDraft
{
    internal string Name { get; init; } = string.Empty;
    internal IptvSourceKind Kind { get; init; }
    internal string Url { get; init; } = string.Empty;
    internal string Username { get; init; } = string.Empty;
    internal string Password { get; init; } = string.Empty;
    internal string MacAddress { get; init; } = string.Empty;
    internal string Serial { get; init; } = string.Empty;
    internal string EpgUrl { get; init; } = string.Empty;
    internal bool PreferHls { get; init; }

    /// <summary>The source this draft describes, with every text field trimmed but the password left as
    /// typed. The id and created time are left at their defaults for the store to fill in.</summary>
    internal IptvSource ToSource() => new()
    {
        Name = Name.Trim(),
        Kind = Kind,
        Url = Url.Trim(),
        Username = Username.Trim(),
        Password = Password,
        MacAddress = MacAddress.Trim(),
        Serial = Serial.Trim(),
        EpgUrl = EpgUrl.Trim(),
        PreferHls = PreferHls,
    };

    /// <summary>A draft pre-filled from a saved source, for the edit form.</summary>
    internal static IptvSourceDraft From(IptvSource source) => new()
    {
        Name = source.Name,
        Kind = source.Kind,
        Url = source.Url,
        Username = source.Username,
        Password = source.Password,
        MacAddress = source.MacAddress,
        Serial = source.Serial,
        EpgUrl = source.EpgUrl,
        PreferHls = source.PreferHls,
    };
}

/// <summary>A group a source's channels are divided into: a live-TV genre, a film genre, or a series genre.</summary>
/// <param name="Id">The provider's id for the category, or the group name itself for a plain M3U.</param>
/// <param name="Name">What the category is called.</param>
/// <param name="Kind">Whether it holds live channels, films, or series.</param>
internal readonly record struct IptvCategory(string Id, string Name, StreamKind Kind);

/// <summary>One playable channel, film, or episode, from any kind of source.</summary>
///
/// <remarks>
/// This is the shape the channel browser and the player work in, whatever the source was. An M3U entry, an
/// Xtream live stream and a Stalker channel all map to this. <see cref="Url"/> is filled in when it is known
/// ahead of time (M3U, Xtream); for a Stalker channel it is null and <see cref="StalkerCmd"/> holds the token
/// the portal turns into a short-lived address at the moment of playing.
/// </remarks>
/// <param name="Id">The provider's id for the item, unique within its source.</param>
/// <param name="Name">What the item is called.</param>
/// <param name="Number">The channel number the provider assigns, or null.</param>
/// <param name="CategoryId">The <see cref="IptvCategory.Id"/> this item belongs to.</param>
/// <param name="Kind">Whether it is a live channel, a film, or an episode.</param>
/// <param name="EpgId">The programme-guide id, matched against XMLTV, or null.</param>
/// <param name="Logo">A web address for the item's logo, or null.</param>
/// <param name="Url">The address to play, when known ahead of time, or null when it must be resolved.</param>
/// <param name="StalkerCmd">The Stalker command that resolves to an address at play time, or null.</param>
internal readonly record struct IptvChannel(
    string Id,
    string Name,
    string? Number,
    string CategoryId,
    StreamKind Kind,
    string? EpgId,
    string? Logo,
    string? Url,
    string? StalkerCmd);
