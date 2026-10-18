using Microsoft.AspNetCore.Mvc;

namespace Asahi.WebServices.ThemesProxy;

/// <summary>
/// The AnimeThemes Proxy controller.
/// </summary>
[ApiController]
public class ThemesProxyController(UrlSignatureVerifierService urlSignatureVerifier, ThemesProxyService themeProxyService) : ControllerBase
{
    /// <summary>
    /// Gets a theme video from AnimeThemes.
    /// </summary>
    /// <param name="themeSlug">The video slug.</param>
    /// <param name="sig">HMAC signature of the slug.</param>
    /// <returns>A redirect to a copy of the video.</returns>
    [HttpGet]
    [Route("/api/theme/{themeSlug}.webm")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult> GetTheme(string themeSlug, [FromQuery] string? sig)
    {
        if (!urlSignatureVerifier.IsAuthorized(UrlSignature.UrlSignaturePurposes.ThemeProxy, themeSlug, sig))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!CompiledRegex.AnimeThemesSlugRegex().IsMatch(themeSlug))
        {
            ModelState.AddModelError(nameof(themeSlug), "Must be a valid AnimeThemes slug.");

            return ValidationProblem();
        }

        Uri? uri;
        try
        {
            uri = await themeProxyService.GetOrCacheThemeAsync(themeSlug);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout);
        }

        if (uri == null)
            return NotFound();

        return Redirect(uri.ToString());
    }
}