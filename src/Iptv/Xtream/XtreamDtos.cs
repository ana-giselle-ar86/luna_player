using System.Text.Json.Serialization;

namespace LunaPlayer.Iptv;

/// <summary>The Xtream Codes wire types, deserialized from the provider's <c>player_api.php</c> responses.</summary>
///
/// <remarks>
/// Every field a provider returns as a number is read leniently: <see cref="IptvJsonContext"/> sets
/// <see cref="JsonNumberHandling.AllowReadingFromString"/> because real Xtream panels return ids and numbers
/// as JSON strings about as often as numbers, and one that insists on a single shape fails on half of them.
/// Only the fields the loader actually uses are declared; the many others each response carries are ignored.
/// </remarks>
internal sealed class XtreamAuthResponse
{
    [JsonPropertyName("user_info")] public XtreamUserInfo? UserInfo { get; set; }
}

internal sealed class XtreamUserInfo
{
    /// <summary>1 when the username and password were accepted, 0 when they were not.</summary>
    [JsonPropertyName("auth")] public int Auth { get; set; }

    /// <summary>"Active", "Expired", "Disabled" or "Banned"; anything but active blocks loading.</summary>
    [JsonPropertyName("status")] public string? Status { get; set; }
}

internal sealed class XtreamCategory
{
    [JsonPropertyName("category_id")] public string? CategoryId { get; set; }
    [JsonPropertyName("category_name")] public string? CategoryName { get; set; }
}

internal sealed class XtreamStream
{
    [JsonPropertyName("num")] public long Num { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("stream_id")] public long StreamId { get; set; }
    [JsonPropertyName("stream_icon")] public string? StreamIcon { get; set; }
    [JsonPropertyName("epg_channel_id")] public string? EpgChannelId { get; set; }
    [JsonPropertyName("category_id")] public string? CategoryId { get; set; }

    /// <summary>The file extension a video-on-demand stream plays as (<c>mp4</c>, <c>mkv</c>); absent for live
    /// streams, which the loader opens as <c>.ts</c> or <c>.m3u8</c> instead.</summary>
    [JsonPropertyName("container_extension")] public string? ContainerExtension { get; set; }
}
