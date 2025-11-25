using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Asahi.WebServices.Controllers;

/// <summary>
/// Health-related info about the service.
/// </summary>
/// <returns></returns>
[ApiController]
public class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    /// <summary>
    /// Whether this service is healthy or not.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("/api/health")]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth()
    {
        var report = await healthCheckService.CheckHealthAsync();
        
        return report.Status != HealthStatus.Unhealthy ? Ok(report) : StatusCode(StatusCodes.Status503ServiceUnavailable, report);
    }
}