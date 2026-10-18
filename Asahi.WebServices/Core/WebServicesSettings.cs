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
public record UrlSigningSettings
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

/// <summary>
/// The type of storage backend to use.
/// </summary>
public enum StorageType
{
    /// <summary>
    /// Use S3 (and compatibles).
    /// </summary>
    S3
}

/// <summary>
/// Settings for the S3 storage backend.
/// </summary>
public record S3StorageSettings
{
    /// <summary>
    /// The S3 bucket to use.
    /// </summary>
    public required string Bucket { get; init; }
    /// <summary>
    /// The URL to serve to users. Expects a trailing slash.
    /// </summary>
    public required string UserFacingBaseUrl { get; init; }
}

/// <summary>
/// Settings for storage.
/// </summary>
public record StorageSettings
{
    /// <summary>
    /// The storage backend to use.
    /// </summary>
    public required StorageType StorageType { get; init; }
    /// <summary>
    /// The maximum allowed total bytes in the storage. May briefly go slightly over before eviction.
    /// </summary>
    public long MaxCachedFileBytes { get; init; } = 10L * 1024 * 1024 * 1024; // 10 GiB
    /// <summary>
    /// Settings for the S3 storage backend.
    /// </summary>
    public S3StorageSettings? S3StorageSettings { get; init; }
}