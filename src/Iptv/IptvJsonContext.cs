using System.Text.Json.Serialization;

namespace LunaPlayer.Iptv;

/// <summary>The source-generated serializer for everything the IPTV subsystem reads or writes as JSON.</summary>
///
/// <remarks>
/// NativeAOT forbids the reflection-based <see cref="System.Text.Json.JsonSerializer"/> overloads, so every
/// type that crosses JSON - the saved-sources file here, and the Xtream and Stalker wire types declared on
/// further <c>partial</c> halves of this class beside their own definitions - must be registered with a
/// <see cref="JsonSerializableAttribute"/>. <see cref="JsonNumberHandling.AllowReadingFromString"/> is set
/// because provider APIs return numeric ids as strings about as often as numbers, and a reader that insists
/// on one shape fails on half of them.
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(IptvSourceDocument))]
[JsonSerializable(typeof(XtreamAuthResponse))]
[JsonSerializable(typeof(XtreamCategory[]))]
[JsonSerializable(typeof(XtreamStream[]))]
[JsonSerializable(typeof(StalkerHandshakeResponse))]
[JsonSerializable(typeof(StalkerGenresResponse))]
[JsonSerializable(typeof(StalkerListResponse))]
[JsonSerializable(typeof(StalkerLinkResponse))]
internal partial class IptvJsonContext : JsonSerializerContext;
