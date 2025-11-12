using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

// TODO: this should ideally support setting user agent, referer, and cloudflare captchas
namespace Asahi.WebServices.Controllers;

/// <summary>
/// The Proxy controller.
/// </summary>
[ApiController]
public class ProxyController(AllowedDomainsService allowedDomainsService, HttpClient client) : ControllerBase
{
    /// <summary>
    /// Returns the content of the given URL.
    /// </summary>
    /// <param name="base64Url">The base64-encoded URL to proxy.</param>
    /// <returns>A 360p PNG thumbnail from the video, captured at 20 seconds in.</returns>
    /// <response code="200">Returns the proxied content.</response>
    /// <response code="400">The provided URL is invalid or not allowed.</response>
    [HttpGet]
    [Route("/api/proxy/{base64Url}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest,
        "application/problem+json")]
    public async Task<ActionResult> ProxyGet([FromRoute] string base64Url)
    {
        if (!Base64Url.IsValid(base64Url))
        {
            var thing = new ModelStateDictionary();
            thing.AddModelError(nameof(base64Url), "Must be a valid base64url string.");

            return ValidationProblem(thing);
        }

        var decodedUrl = Base64Url.DecodeFromChars(base64Url);
        var url = Encoding.UTF8.GetString(decodedUrl);

        if (!allowedDomainsService.IsDomainAllowed(url))
        {
            var allowedDomainsString =
                string.Join(';', allowedDomainsService.AllowedDomainRegexStrings);

            ModelState.AddModelError(nameof(base64Url),
                $"Disallowed URL. Allowed domain regexes: {allowedDomainsString}");

            return ValidationProblem();
        }

        var req = new HttpRequestMessage(HttpMethod.Get, url);

        if (Request.Headers.IfNoneMatch.Count > 0)
        {
            req.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(Request.Headers.IfNoneMatch!));
        }

        var res = await client.SendAsync(req);

        Response.Headers.ETag = res.Headers.ETag?.ToString();
        Response.Headers.CacheControl = res.Headers.CacheControl?.ToString();

        if (res.StatusCode == HttpStatusCode.NotModified)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }
        else if (!res.IsSuccessStatusCode)
        {
            return Problem(res.ReasonPhrase);
        }

        return File(await res.Content.ReadAsStreamAsync(),
            res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
    }
}