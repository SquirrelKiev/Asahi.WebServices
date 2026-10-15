using System.Collections.Frozen;
using Microsoft.Extensions.Options;

namespace Asahi.WebServices;

/// <summary>
/// A service for verifying URL signatures against configured keys.
/// </summary>
public class UrlSignatureVerifierService
{
    private record State(
        FrozenDictionary<string, byte[]> Keys,
        UnsignedUrlPolicy UnsignedPolicy,
        FrozenSet<string> AllowedUrls);

    private readonly IHostEnvironment environment;
    private State state;
    private readonly ILogger<UrlSignatureVerifierService> logger;

    /// <summary>Creates an instance of <see cref="UrlSignatureVerifierService"/>.</summary>
    public UrlSignatureVerifierService(IOptionsMonitor<UrlSigningSettings> options, IHostEnvironment environment, ILogger<UrlSignatureVerifierService> logger)
    {
        this.environment = environment;
        this.logger = logger;
        
        state = BuildState(options.CurrentValue);
        
        options.OnChange(OnSettingsChanged);
    }
    
    private void OnSettingsChanged(UrlSigningSettings settings)
    {
        try
        {
            state = BuildState(settings);
            logger.LogInformation("UrlSigning settings changed.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update UrlSigning settings.");
            throw;
        }
    }
    
    private State BuildState(UrlSigningSettings settings)
    {
        var keys = settings.Keys
            .Where(x => !string.IsNullOrEmpty(x.Key) && !string.IsNullOrEmpty(x.Value))
            .ToFrozenDictionary(x => x.Key, x => DecodeKey(x.Key, x.Value));

        foreach (var keyId in keys.Keys)
        {
            if (!UrlSignature.IsValidKeyId(keyId))
            {
                throw new InvalidOperationException(
                    $"UrlSigning:Keys:{keyId} is not a valid key ID. Key IDs must be URL safe.");
            }
        }

        if (keys.Count == 0 && settings.UnsignedPolicy != UnsignedUrlPolicy.AllowAll)
        {
            throw new InvalidOperationException(
                "UrlSigning:Keys is empty, despite signatures being required by config.");
        }

        var unsignedPolicy = settings.UnsignedPolicy;
        var allowedUrls = unsignedPolicy == UnsignedUrlPolicy.AllowList && settings.AllowListPath != null
            ? File.ReadLines(Path.Combine(environment.ContentRootPath, settings.AllowListPath)).ToFrozenSet()
            : FrozenSet<string>.Empty;
        
        return new State(keys, unsignedPolicy, allowedUrls);
    }

    /// <summary>
    /// Determines whether a request is authorized based on the URL signature and configured policies.
    /// </summary>
    /// <param name="purpose">Which API the signature is intended for.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="signature"> The signature provided for authentication and verification. Can be null if unsigned URLs are allowed. </param>
    /// <returns>
    /// Returns true if the request is authorized; otherwise, false.
    /// </returns>
    public bool IsAuthorized(UrlSignature.UrlSignaturePurposes purpose, string resource, string? signature)
    {
        var stateSnapshot = state;
        
        if (!string.IsNullOrEmpty(signature)) return UrlSignature.Verify(stateSnapshot.Keys, purpose, resource, signature);

        switch (stateSnapshot.UnsignedPolicy)
        {
            case UnsignedUrlPolicy.AllowAll:
                return true;
            case UnsignedUrlPolicy.AllowList:
                return stateSnapshot.AllowedUrls.Contains($"{UrlSignature.PurposeToString(purpose)},{resource}");
            case UnsignedUrlPolicy.None:
            default:
                return false;
        }
    }

    private static byte[] DecodeKey(string keyId, string value)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"UrlSigning:Keys:{keyId} is not a valid base64 string.", ex);
        }

        if (key.Length == 0)
        {
            throw new InvalidOperationException($"UrlSigning:Keys:{keyId} is empty.");
        }

        return key;
    }
}