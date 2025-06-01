using Microsoft.AspNetCore.Mvc;

namespace Asahi.WebServices.Controllers;

/// <summary>
/// Controller for the "/" route. 
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public class RootController : ControllerBase
{
    /// <summary>
    /// Simply redirects you to Scalar.
    /// </summary>
    [HttpGet]
    [Route("/")]
    [ProducesResponseType(StatusCodes.Status301MovedPermanently)]
    public ActionResult RootGet()
    {
        return RedirectPermanent("/scalar");
    }
}