using System.Text.Json.Serialization;

namespace LunaPlayer.Iptv;

/// <summary>The Stalker (MAG set-top-box portal) wire types. Every response wraps its payload in a <c>js</c>
/// envelope, so each type here mirrors that: an outer holder with a single <c>js</c> field.</summary>
///
/// <remarks>
/// The Stalker protocol is undocumented and varies between panels; these cover the handshake, profile, genre
/// list, channel list and link-resolution steps the loader uses, and nothing more. Number-shaped fields are
/// read leniently through <see cref="IptvJsonContext"/>'s
/// <see cref="System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString"/>.
/// </remarks>
internal sealed class StalkerHandshakeResponse
{
    [JsonPropertyName("js")] public StalkerToken? Js { get; set; }
}

internal sealed class StalkerToken
{
    [JsonPropertyName("token")] public string? Token { get; set; }
}

internal sealed class StalkerGenresResponse
{
    [JsonPropertyName("js")] public StalkerGenre[]? Js { get; set; }
}

internal sealed class StalkerGenre
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
}

internal sealed class StalkerListResponse
{
    [JsonPropertyName("js")] public StalkerList? Js { get; set; }
}

internal sealed class StalkerList
{
    [JsonPropertyName("total_items")] public int TotalItems { get; set; }
    [JsonPropertyName("max_page_items")] public int MaxPageItems { get; set; }
    [JsonPropertyName("data")] public StalkerChannel[]? Data { get; set; }
}

internal sealed class StalkerChannel
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }

    /// <summary>The token the portal turns into a short-lived playable address through <c>create_link</c>.</summary>
    [JsonPropertyName("cmd")] public string? Cmd { get; set; }

    [JsonPropertyName("tv_genre_id")] public string? GenreId { get; set; }
    [JsonPropertyName("logo")] public string? Logo { get; set; }
    [JsonPropertyName("xmltv_id")] public string? XmltvId { get; set; }
}

internal sealed class StalkerLinkResponse
{
    [JsonPropertyName("js")] public StalkerLink? Js { get; set; }
}

internal sealed class StalkerLink
{
    [JsonPropertyName("cmd")] public string? Cmd { get; set; }
}
