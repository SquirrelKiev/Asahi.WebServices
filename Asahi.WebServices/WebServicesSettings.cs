using Microsoft.OpenApi;

namespace Asahi.WebServices;

/// <summary>
/// Settings for OpenAPI.
/// </summary>
public record OpenApiSettings
{
    /// <summary>
    /// A list of server endpoints.
    /// </summary>
    public List<OpenApiServer> Servers { get; init; } = [];
}

/// <summary>
/// Settings for <see cref="Asahi.WebServices.AllowedDomainsService"/>.
/// </summary>
public record AllowedDomainsSettings
{
    /// <summary>
    /// A list of regex strings that represent allowed domains.
    /// </summary>
    public List<string> Regexes { get; init; } = [];
}

/// <summary>
/// How to enforce unsigned URLs.
/// </summary>
public enum UnsignedUrlPolicy
{
    /// <summary>Ignore missing signatures.</summary>
    AllowAll,
    /// <summary>Let some unsigned URLs through.</summary>
    AllowList,
    /// <summary>Do not allow any unsigned URLs. AKA, enforce that signatures are always present.</summary>
    None
}

/// <summary>
/// Settings for <see cref="UrlSignatureVerifierService"/>.
/// </summary>
public class UrlSigningSettings
{
    /// <summary>
    /// A list of accepted signing keys.
    /// </summary>
    public Dictionary<string, string> Keys { get; init; } = [];
    
    /// <summary>
    /// How to enforce unsigned URLs.
    /// </summary>
    public UnsignedUrlPolicy UnsignedPolicy { get; init; } = UnsignedUrlPolicy.AllowAll;
    
    /// <summary>
    /// Path to a file containing a list of allowed unsigned URLs. Only takes effect if <see cref="UnsignedPolicy"/> is <see cref="UnsignedUrlPolicy.AllowList"/>.
    /// </summary>
    public string? AllowListPath { get; init; }
}
