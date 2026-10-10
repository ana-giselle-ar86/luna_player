namespace LunaPlayer.YouTube;

/// <summary>A source of results that can be drawn from a batch at a time.</summary>
///
/// <remarks>
/// The results window pages by asking for more; whether the more comes from a search continuation or a
/// channel tab is not its concern. Both the search paging in <see cref="Metadata.Page"/> and the channel
/// paging in <see cref="Client.ChannelPage"/> answer to this, so one paging path in the window and the
/// session serves both.
/// </remarks>
internal interface IResultPage
{
    /// <summary>Whether another batch might still be waiting. False once a batch has come back short.</summary>
    bool HasMore { get; }

    /// <summary>Fetches up to <paramref name="count"/> more results, an empty list at the end.</summary>
    Task<IReadOnlyList<YouTubeResult>> Take(int count, CancellationToken token);
}
