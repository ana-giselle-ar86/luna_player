namespace LunaPlayer.YouTube;

/// <summary>Whether an operation worked, and what to tell the user when it did not.</summary>
/// <remarks>
/// The same shape as <see cref="LunaPlayer.UI.UiOperation"/>, and for the same reason: a caller that
/// only wants to report a failure should not have to catch anything to find out there was one.
/// </remarks>
/// <param name="Error">Empty when <paramref name="Success"/> is true; otherwise a message already in
/// the user's language, ready to show.</param>
internal readonly record struct YouTubeOutcome(bool Success, string Error = "")
{
    internal static YouTubeOutcome Ok { get; } = new(true);
}
