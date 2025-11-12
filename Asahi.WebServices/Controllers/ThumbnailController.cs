using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Hashing;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Asahi.WebServices.Controllers;

/// <summary>
/// Generates thumbnails for videos. Ensures that a thumbnail is only generated once for a given video URL.
/// </summary>
public class ThumbnailGenerator(ILogger<ThumbnailGenerator> logger)
{
    // Lazy is here because it turns out that ConcurrentDictionary.GetOrAdd can run the value factory multiple times
    // for the same key (publication only)
    // Lazy, on the other hand, is thread safe on both execution and publication
    private readonly ConcurrentDictionary<string, Lazy<Task<ThumbnailEntry>>> ongoingRequests = [];
    private readonly MemoryCache cache = new(new MemoryCacheOptions() { SizeLimit = 1000 * 1000 * 1000 }); // 1GB

    /// <summary>
    /// Retrieves a cached thumbnail for the given URL.
    /// </summary>
    /// <param name="url">The URL to retrieve the cached result from</param>
    /// <returns></returns>
    public ThumbnailEntry GetCachedThumbnail(string url)
    {
        if (cache.TryGetValue(url, out ThumbnailEntry cachedThumbnail))
        {
            return cachedThumbnail;
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// Generates a thumbnail for the given video URL. If a thumbnail has already been generated for the given URL within the last hour,
    /// it returns the cached thumbnail instead.
    /// </summary>
    /// <param name="url">The URL of the video.</param>
    /// <returns>A 360p PNG thumbnail.</returns>
    public Task<ThumbnailEntry> GetThumbnailAsync(string url)
    {
        if (cache.TryGetValue(url, out ThumbnailEntry cachedThumbnail))
        {
            logger.LogInformation("Found cached thumbnail for url {url}", url);

            return Task.FromResult(cachedThumbnail);
        }

        var lazyTask = new Lazy<Task<ThumbnailEntry>>(async () =>
        {
            try
            {
                var val = await GenerateThumbnailAsync(url);

                if (val != default)
                {
                    cache.Set(url, val, new MemoryCacheEntryOptions()
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                        Size = val.Thumbnail.Length
                    });
                }

                return val;
            }
            finally
            {
                ongoingRequests.Remove(url, out _);
            }
        });

        var runningLazyTask = ongoingRequests.GetOrAdd(url, lazyTask);

        if (ReferenceEquals(runningLazyTask, lazyTask))
        {
            logger.LogInformation("Starting new thumbnail generation for url {url}", url);
        }
        else
        {
            logger.LogInformation("Waiting for ongoing generation of thumbnail for url {url}", url);
        }

        return runningLazyTask.Value;
    }

    private async Task<ThumbnailEntry> GenerateThumbnailAsync(string url)
    {
        // optimizing for speed not size here, so doing straight to png.
        // in the context of web, output seeking is actually faster than input seeking for this timestamp from my benchmarks
        // output (`-i url -ss ...`) 1.5s~ give or take, input (`-ss ... -i url`) 4.5s~
        // all of this was with a timestamp of 10s (validated with 20s to have the same problem, just longer)
        // my assumption is due to only doing 1 web request instead of like 3
        // not sure what a good timestamp is but
        // * 1 second in seems to give poor results
        // * 10 seconds is better but not representative
        // * 20 seconds seems good enough
        // this should be fine for now
        var processInfo = new ProcessStartInfo("ffmpeg.exe",
        [
            "-hide_banner", "-loglevel", "error", "-i", url, "-ss", "00:00:20", "-frames:v", "1", "-vf", "scale=-1:360",
            "-f", "image2pipe", "-vcodec", "png",
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

            logger.LogError("Failed to generate thumbnail for url {url}, code {code}: {error}", url, process.ExitCode,
                error);

            return default;
        }

        var thumbnailData = memoryStream.ToArray();
        return new ThumbnailEntry(url, thumbnailData, XxHash3.HashToUInt64(thumbnailData));
    }

    /// <summary>
    /// A thumbnail and relevant data.
    /// </summary>
    /// <param name="Url"></param>
    /// <param name="Thumbnail"></param>
    /// <param name="Hash"></param>
    public readonly record struct ThumbnailEntry(string Url, byte[] Thumbnail, ulong Hash);
}

/// <summary>
/// The thumbnail controller.
/// </summary>
[ApiController]
public class ThumbnailController(
    AllowedDomainsService allowedDomainsService,
    ThumbnailGenerator thumbGenerator) : ControllerBase
{
    /// <summary>
    /// Generates a thumbnail image from a video URL.
    /// </summary>
    /// <param name="base64Url">The base64url-encoded video URL to generate a thumbnail from. Currently only allows animethemes.moe WEBMs.</param>
    /// <returns>A 360p PNG thumbnail from the video, captured at 20 seconds in.</returns>
    /// <response code="200">Returns the generated thumbnail as a PNG image</response>
    /// <response code="400">The provided URL is invalid or not supported</response>
    [HttpGet]
    [Route("/api/thumb/{base64Url}.png")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK, "image/png")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest,
        "application/problem+json")]
    // output cache was causing strange issues, rolling my own
    // [OutputCache(VaryByRouteValueNames = [nameof(base64Url)], Duration = 60 * 60)] // 1 hour
    public async Task<ActionResult<byte[]>> GetThumbnail([FromRoute] string base64Url)
    {
        if (!Base64Url.IsValid(base64Url))
        {
            ModelState.AddModelError(nameof(base64Url), "Must be a valid base64url string.");

            return ValidationProblem();
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