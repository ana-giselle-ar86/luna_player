using System.Security.Cryptography;
using System.Text;

namespace LunaPlayer.Iptv;

/// <summary>Protects the credential fields of a saved IPTV source at rest, so a password does not sit in plain
/// text in a file under the user's profile.</summary>
///
/// <remarks>
/// Windows DPAPI (<see cref="ProtectedData"/>, <see cref="DataProtectionScope.CurrentUser"/>) does the work:
/// the ciphertext can be read back only by the same Windows user on the same machine, and there is no key for
/// the app to store or lose. A protected value is written as <c>dpapi:</c> followed by base64; a value without
/// that prefix is read back as-is, so a file written before this existed still loads and is re-protected the
/// next time it is saved.
///
/// Best-effort, and never a reason a save or load fails: if protecting throws - a platform without DPAPI, an
/// unusual account state - the value is stored in the clear rather than lost, and if unprotecting throws - a
/// file copied from another user or machine - the field comes back empty so the user is prompted to type it
/// again rather than the whole store refusing to load. Values pass through here as strings and are never
/// logged.
/// </remarks>
internal static class CredentialProtection
{
    private const string Marker = "dpapi:";

    // Bound to this application so a protected value cannot be lifted into another DPAPI-using program running
    // as the same user. Not a secret; it only namespaces the protection.
    private static readonly byte[] Entropy = "LunaPlayer.Iptv.v1"u8.ToArray();

    /// <summary>The stored form of a credential: DPAPI-protected and marked, or the value unchanged when it is
    /// empty or protection is not available.</summary>
    internal static string Protect(string? value)
    {
        if (value is not { Length: > 0 })
            return string.Empty;
        try
        {
            var cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
            return Marker + Convert.ToBase64String(cipher);
        }
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
        {
            return value;
        }
    }

    /// <summary>The usable form of a stored credential: unprotected when it was protected, unchanged when it
    /// was stored in the clear, or empty when protected data cannot be read back on this machine or account.
    /// </summary>
    internal static string Unprotect(string? stored)
    {
        if (stored is not { Length: > 0 })
            return string.Empty;
        if (!stored.StartsWith(Marker, StringComparison.Ordinal))
            return stored;
        try
        {
            var cipher = Convert.FromBase64String(stored[Marker.Length..]);
            var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException
            or PlatformNotSupportedException)
        {
            return string.Empty;
        }
    }
}
