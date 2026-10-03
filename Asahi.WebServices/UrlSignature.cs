using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Asahi;

/// <summary>
/// Generates and verifies URL signatures for resources.
/// </summary>
public static partial class UrlSignature
{
    /// <summary>
    /// Checks whether a key ID can be safely embedded in a URL.
    /// </summary>
    /// <param name="keyId">The key ID to check.</param>
    /// <returns>Whether the key ID is safe.</returns>
    public static bool IsValidKeyId(string keyId) => KeyIdRegex().IsMatch(keyId);

    [GeneratedRegex(@"^[A-Za-z0-9._-]+\z")]
    private static partial Regex KeyIdRegex();

    /// <summary>
    /// Defines which API the URL signature is for.
    /// </summary>
    public enum UrlSignaturePurposes
    {
        /// <summary>
        /// The thumbnail API.
        /// </summary>
        Thumbnail,
        /// <summary>
        /// The generic proxy API.
        /// </summary>
        Proxy
    }

    /// <summary>
    /// Converts a specified <see cref="UrlSignaturePurposes"/> value to its corresponding string representation.
    /// Intended for use in generated URL signatures.
    /// </summary>
    /// <param name="purpose">The purpose to convert.</param>
    /// <returns>A string representation of the specified <see cref="UrlSignaturePurposes"/> value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified <paramref name="purpose"/> is not a valid value of <see cref="UrlSignaturePurposes"/>.</exception>
    /// <remarks>the main point is to keep the string representation stable in case the enum changes for whatever reason</remarks>
    public static string PurposeToString(UrlSignaturePurposes purpose) =>
        purpose switch
        {
            UrlSignaturePurposes.Thumbnail => "thumbnail",
            UrlSignaturePurposes.Proxy => "proxy",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };

    /// <summary>
    /// Generates a signature for a resource.
    /// </summary>
    /// <param name="keyId">The identifier for the key used to generate the signature.</param>
    /// <param name="key">The key used for signing the resource.</param>
    /// <param name="purpose">The API this signature is intended to be used on.</param>
    /// <param name="resource">The resource the signature is for.</param>
    /// <returns>The URL signature in the format <c>{keyId}.{signature}</c>. Can be provided as a query parameter as-is.</returns>
    public static string Create(string keyId, ReadOnlySpan<byte> key, UrlSignaturePurposes purpose, string resource)
    {
        byte[] mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{PurposeToString(purpose)}:{resource}"));
        return $"{keyId}.{Base64Url.EncodeToString(mac)}";
    }

    /// <summary>
    /// Verifies a URL signature for a given resource.
    /// </summary>
    /// <param name="keys">A collection of key identifiers and their corresponding keys.</param>
    /// <param name="purpose">The API this signature is intended to be used on.</param>
    /// <param name="resource">The resource the signature is for.</param>
    /// <param name="signature">The URL signature to validate, in the format <c>{keyId}.{signature}</c>.</param>
    /// <returns>Whether the signature is valid.</returns>
    public static bool Verify(IReadOnlyDictionary<string, byte[]> keys, UrlSignaturePurposes purpose, string resource,
        string signature)
    {
        int dot = signature.LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        string keyId = signature[..dot];
        if (!keys.TryGetValue(keyId, out byte[]? key))
        {
            return false;
        }
        
        string expectedSig = Create(keyId, key, purpose, resource);
        // FixedTimeEquals is probably not necessary given any amount of jitter, but It's Good Practice™
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature), Encoding.ASCII.GetBytes(expectedSig));
    }
}
