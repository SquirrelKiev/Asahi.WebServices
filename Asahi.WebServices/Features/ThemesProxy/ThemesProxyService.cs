using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Mime;
using Asahi.WebServices.Database;
using Asahi.WebServices.Database.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Asahi.WebServices.ThemesProxy;

/// <summary>
/// Service for caching and retrieving themes from AnimeThemes.
/// </summary>
public class ThemesProxyService(
    IStorage storage,
    IHttpClientFactory httpClientFactory,
    IClock clock,
    IDbContextFactory<AsahiDbContext> dbContextFactory,
    ThemeCacheEvictor cacheEvictor,
    ILogger<ThemesProxyService> logger)
{
    /// <summary>
    /// The name of the <see cref="HttpClient"/> the service uses. For DI.
    /// </summary>
    public const string ThemesHttpClientName = "animethemes-video-cdn";

    private const string StoragePrefix = "themes";
    private readonly ConcurrentDictionary<string, Lazy<Task<Uri?>>> ongoingRequests = [];

    /// <summary>
    /// Gets a theme URL from cache, or if it isn't cached, caches the video and then returns it.
    /// </summary>
    /// <param name="slug">The slug of the video to retrieve.</param>
    /// <returns>The theme URL, or <see langword="null"/> if the theme doesn't exist.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the request to AnimeThemes times out.</exception>
    public async Task<Uri?> GetOrCacheThemeAsync(string slug)
    {
        await using var dbContext = dbContextFactory.CreateDbContext();

        var now = clock.GetCurrentInstant();
        var cacheHit = await dbContext.CachedThemes
            .Where(x => x.ThemeSlug == slug)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastRetrievedAt, now)) > 0;
        if (cacheHit)
        {
            try
            {
                var uri = await storage.GetLinkToFileAsync(SlugToPath(slug));
                return uri;
            }
            catch (FileNotFoundException)
            {
                // something very wrong has happened
                await dbContext.CachedThemes.Where(x => x.ThemeSlug == slug).ExecuteDeleteAsync();
            }
        }

        var lazyTask = new Lazy<Task<Uri?>>(async () =>
        {
            try
            {
                return await ForceCacheThemeAsync(slug);
            }
            finally
            {
                ongoingRequests.Remove(slug, out _);
            }
        });

        var runningLazyTask = ongoingRequests.GetOrAdd(slug, lazyTask);

        if (ReferenceEquals(runningLazyTask, lazyTask))
        {
            logger.LogInformation("Caching video {Slug}", slug);
        }
        else
        {
            logger.LogInformation("Waiting for ongoing caching for {Slug}", slug);
        }

        return await runningLazyTask.Value;
    }

    /// <summary>
    /// Downloads a theme and caches it in storage.
    /// </summary>
    /// <param name="slug">The slug of the video to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cached theme URL, or <see langword="null"/> if the theme doesn't exist.</returns>
    private async Task<Uri?> ForceCacheThemeAsync(string slug, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ThemesHttpClientName);

        using var res = await client.GetAsync($"/{slug}.webm", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (res.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        res.EnsureSuccessStatusCode();

        if(!res.Content.Headers.ContentLength.HasValue)
            throw new InvalidOperationException($"Content length not set for {res.RequestMessage?.RequestUri}");
        
        var contentLength = res.Content.Headers.ContentLength.Value;

        var resStream = await res.Content.ReadAsStreamAsync(cancellationToken);

        using var timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutToken.CancelAfter(TimeSpan.FromSeconds(120));
        
        var storagePath = SlugToPath(slug);
        await storage.WriteFileAsync(storagePath, resStream, "video/webm", contentLength, timeoutToken.Token);
        var uri = await storage.GetLinkToFileAsync(storagePath, cancellationToken);

        await using var dbContext = dbContextFactory.CreateDbContext();
        var existingEntry = await dbContext.CachedThemes.FirstOrDefaultAsync(x => x.ThemeSlug == slug, cancellationToken: cancellationToken);
        if (existingEntry == null)
        {
            existingEntry = new CachedTheme()
            {
                ThemeSlug = slug,
                SavedAt = clock.GetCurrentInstant(),
                LastRetrievedAt = clock.GetCurrentInstant(),
                FileSizeBytes = contentLength
            };
            dbContext.CachedThemes.Add(existingEntry);
        }

        existingEntry.LastRetrievedAt = clock.GetCurrentInstant();
        await dbContext.SaveChangesAsync(cancellationToken);
        
        cacheEvictor.RequestEviction();

        return uri;
    }

    /// <summary>
    /// Converts the slug into a relative path for storage.
    /// </summary>
    /// <param name="slug">The video slug.</param>
    /// <returns>The relative path to the video file.</returns>
    [Pure]
    public static string SlugToPath(string slug)
    {
        return $"{StoragePrefix}/{slug}.webm";
    }
}