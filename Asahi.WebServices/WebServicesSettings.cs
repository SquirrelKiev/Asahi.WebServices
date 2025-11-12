using Microsoft.OpenApi;

namespace Asahi.WebServices;

/// <summary>
/// OpenAPI customizable settings.
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