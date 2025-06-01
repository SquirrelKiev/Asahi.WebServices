using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OutputCaching;

namespace Asahi.WebServices.Controllers;

/// <summary>
/// For DI. Handles storing currently running thumbnail requests.
/// </summary>
public class ThumbnailRequestDeduplicator
{
    private readonly ConcurrentDictionary<int, Task<byte[]?>> ongoingRequests = [];

    /// <summary>
    /// Stores a currently running thumbnail request by using the specified function and an argument
    /// if there is not currently a thumbnail request operation running, or returns the existing task if there is.
    /// </summary>
    /// <param name="url">The URL of the video.</param>
    /// <param name="factory">The function used to generate the thumbnail.</param>
    /// <returns></returns>
    public async Task<byte[]?> GetOrAddAsync(string url, Func<Task<byte[]?>> factory)
    {
        // not sure if GetHashCode will cause issues, but given this is only for this process its probably fine
        var urlHash = url.GetHashCode();

        var task = ongoingRequests.GetOrAdd(urlHash, static (_, fac) => fac(), factory);

        try
        {
            return await task;
        }
        finally
        {
            ongoingRequests.TryRemove(urlHash, out _);
        }
    }
}

/// <summary>
/// The thumbnail controller.
/// </summary>
[ApiController]
public class ThumbnailController(
    ILogger<ThumbnailController> logger,
    ThumbnailRequestDeduplicator deduplicator) : ControllerBase
{
    /// <summary>
    /// Generates a thumbnail image from a video URL.
    /// </summary>
    /// <param name="base64Url">The base64url-encoded video URL to generate a thumbnail from. Currently only allows animethemes.moe WEBMs.</param>
    /// <returns>A 360p PNG thumbnail from the video, captured at 20 seconds in.</returns>
    /// <response code="200">Returns the generated thumbnail as a PNG image</response>
    /// <response code="400">The provided URL is invalid or not supported</response>
    /// <response code="500">An error occurred during thumbnail generation</response>
    [HttpGet]
    [Route("/api/thumb/{base64Url}.png")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK, "image/png")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError, "application/problem+json")]
    [OutputCache(VaryByRouteValueNames = [nameof(base64Url)])]
    public async Task<ActionResult<byte[]>> GetThumbnail([FromRoute] string base64Url)
    {
        if (!Base64Url.IsValid(base64Url))
        {
            var thing = new ModelStateDictionary();
            thing.AddModelError(nameof(base64Url), "Must be a valid base64url string.");

            return ValidationProblem(thing);
        }
        
        var decodedUrl = Base64Url.DecodeFromChars(base64Url);
        var url = Encoding.UTF8.GetString(decodedUrl);

        if (!CompiledRegex.AnimeThemesThemeRegex().IsMatch(url))
        {
            var thing = new ModelStateDictionary();
            thing.AddModelError(nameof(base64Url), "Invalid URL. Only animethemes.moe is supported at the moment.");

            return ValidationProblem(thing);
        }

        var thumbnail = await deduplicator.GetOrAddAsync(url, () => GenerateThumbnailAsync(url));

        if (thumbnail == null)
        {
            return Problem("Failed to generate thumbnail. Does the requested video exist?");
        }
        else
        {
            Response.Headers.ETag = string.Concat(SHA1.HashData(thumbnail).Select(x => x.ToString("x2")));
            return File(thumbnail, "image/png");
        }
    }

    private async Task<byte[]?> GenerateThumbnailAsync(string url)
    {
        // optimizing for speed not size here, so doing straight to png.
        // in the context of web, output seeking is actually faster than input seeking for this timestamp from my benchmarks
        // output (`-i url -ss ...`) 1.5s~ give or take, input (`-ss ... -i url`) 4.5s~
        // all of this was with a timestamp of 10s
        // my assumption is due to only doing 1 web request instead of like 3
        // not sure what a good timestamp is but 1 second in seems to give poor results, 10 seconds is better but poor, 20 seconds is good enough
        // this should be fine for now
        var processInfo = new ProcessStartInfo("ffmpeg.exe",
        [
            "-hide_banner", "-loglevel", "error", "-i", url, "-ss", "00:00:20", "-frames:v", "1", "-vf", "scale=-1:360", "-f", "image2pipe", "-vcodec", "png",
            "-"
        ])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = processInfo;

        process.Start();

        using var memoryStream = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(memoryStream);

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();

            logger.LogError("Failed to generate thumbnail for url {url}, code {code}: {error}", url, process.ExitCode, error);

            return null;
        }

        var thumbnailData = memoryStream.ToArray();
        return thumbnailData;
    }
}