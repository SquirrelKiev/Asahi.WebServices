using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Asahi.WebServices;

/// <summary>
/// Service for checking if a given URL is allowed.
/// </summary>
public class AllowedDomainsService
{
    private readonly IOptionsMonitor<AllowedDomainsSettings> settings;
    private readonly ILogger<AllowedDomainsService> logger;

    /// <summary>
    /// The current allowed domains.
    /// </summary>
    public IReadOnlyCollection<string> AllowedDomainRegexStrings => settings.CurrentValue.Regexes;
    private IReadOnlyCollection<Regex> allowedDomains = [];

    /// 
    public AllowedDomainsService(IOptionsMonitor<AllowedDomainsSettings> settings, ILogger<AllowedDomainsService> logger)
    {
        this.settings = settings;
        this.logger = logger;

        settings.OnChange(OnSettingsChanged);
        
        OnSettingsChanged(settings.CurrentValue);
    }

    private void OnSettingsChanged(AllowedDomainsSettings obj)
    {
        var newDomains = new Regex[obj.Regexes.Count];

        for (var i = 0; i < obj.Regexes.Count; i++)
        {
            var regex = obj.Regexes[i];

            newDomains[i] = new Regex(regex,
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        allowedDomains = newDomains;
        
        logger.LogInformation("Allowed domains updated.");
    }

    /// <summary>
    /// Checks if the given URL is allowed to be processed.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>Whether the given URL is allowed to be processed.</returns>
    public bool IsDomainAllowed(string url)
    {
        return allowedDomains.Any(regex => regex.IsMatch(url));
    }
}