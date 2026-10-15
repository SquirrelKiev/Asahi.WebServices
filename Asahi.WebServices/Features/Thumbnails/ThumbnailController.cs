using System.Buffers.Text;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Asahi.WebServices.Thumbnails;

/// <summary>
/// The thumbnail controller.
/// </summary>
[ApiController]
public class ThumbnailController(
    AllowedDomainsService allowedDomainsService,
    UrlSignatureVerifierService urlSignatureVerifier,
    ThumbnailGenerator thumbGenerator) : ControllerBase
{
    /// <summary>
    /// Generates a thumbnail image from a video URL.
    /// </summary>
    /// <param name="base64Url">The base64url-encoded video URL to generate a thumbnail from. Currently only allows animethemes.moe WEBMs.</param>
    /// <param name="sig">HMAC signature of the video URL.</param>
    /// <returns>A 360p PNG thumbnail from the video, captured at 20 seconds in.</returns>
    /// <response code="200">Returns the generated thumbnail as a PNG image.</response>
    /// <response code="400">The provided URL is invalid or not supported.</response>
    /// <response code="403">Either the provided signature is invalid, or a signature was not provided and the URL is not allowlisted.</response>
    [HttpGet]
    [Route("/api/thumb/{base64Url}.png")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK, "image/png")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    // output cache was causing strange issues, rolling my own
    // [OutputCache(VaryByRouteValueNames = [nameof(base64Url)], Duration = 60 * 60)] // 1 hour
    public async Task<ActionResult<byte[]>> GetThumbnail([FromRoute] string base64Url, [FromQuery] string? sig)
    {
        if (!Base64Url.IsValid(base64Url))
        {
            ModelState.AddModelError(nameof(base64Url), "Must be a valid base64url string.");

            return ValidationProblem();
        }

        var decodedUrl = Base64Url.DecodeFromChars(base64Url);
        var url = Encoding.UTF8.GetString(decodedUrl);

        if (!urlSignatureVerifier.IsAuthorized(UrlSignature.UrlSignaturePurposes.Thumbnail, url, sig))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!allowedDomainsService.IsDomainAllowed(url))
        {
            var allowedDomainsString =
                string.Join(';', allowedDomainsService.AllowedDomainRegexStrings);

            ModelState.AddModelError(nameof(base64Url),
                $"Disallowed URL. Allowed domain regexes: {allowedDomainsString}");

            return ValidationProblem();
        }

        string? requesterEtag = null;
        if (Request.Headers.IfNoneMatch.Count > 0)
        {
            requesterEtag = Request.Headers.IfNoneMatch.ToString();

            var cachedThumb = thumbGenerator.GetCachedThumbnail(url);

            if (cachedThumb != default && cachedThumb.Hash.ToString("X") == requesterEtag)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }
        }

        var thumbnail = await thumbGenerator.GetThumbnailAsync(url);

        if (thumbnail == default)
        {
            return Problem("Failed to generate thumbnail. Does the requested video exist?");
        }

        if (requesterEtag != null && thumbnail.Hash.ToString("X") == requesterEtag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = string.Concat(thumbnail.Hash.ToString("X"));
        return File(thumbnail.Thumbnail, "image/png");
    }
}