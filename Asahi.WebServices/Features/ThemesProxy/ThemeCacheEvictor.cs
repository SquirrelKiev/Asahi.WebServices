using System.Threading.Channels;
using Asahi.WebServices.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Asahi.WebServices.ThemesProxy;

/// <summary>
/// Service for managing cache eviction for themes storage. Implements an LRU cache.
/// </summary>
public class ThemeCacheEvictor(IStorage storage,
    IClock clock,
    IDbContextFactory<AsahiDbContext> dbContextFactory,
    IOptions<StorageSettings> settings,
    ILogger<ThemeCacheEvictor> logger) : BackgroundService
{
    private readonly Channel<bool> signal = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <summary>
    /// Asks the evictor to check if there are any files that need purging in order to meet the file size requirements.
    /// </summary>
    public void RequestEviction() => signal.Writer.TryWrite(true);
    
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RequestEviction();
        await foreach (var _ in signal.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await EvictAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to evict theme cache");
            }
        }
    }

    private async Task EvictAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = dbContextFactory.CreateDbContext();
        var excess = await dbContext.CachedThemes.SumAsync(x => x.FileSizeBytes, cancellationToken) - settings.Value.MaxCachedFileBytes;
        var cutoff = clock.GetCurrentInstant() - Duration.FromMinutes(5);

        if (excess <= 0)
            return;
        
        var evictionCandidates = await dbContext.CachedThemes
            .AsNoTracking()
            .Where(x => x.LastRetrievedAt < cutoff)
            .OrderBy(x => x.LastRetrievedAt)
            .ToListAsync(cancellationToken);

        foreach (var theme in evictionCandidates)
        {
            // in case we read a row, and before we delete it, it's retrieved
            var deleted = await dbContext.CachedThemes
                .Where(x => x.ThemeSlug == theme.ThemeSlug && x.LastRetrievedAt == theme.LastRetrievedAt)
                .ExecuteDeleteAsync(cancellationToken);
            
            if(deleted == 0)
                continue;
            
            await storage.DeleteFileAsync(ThemesProxyService.SlugToPath(theme.ThemeSlug), cancellationToken);
            excess -= theme.FileSizeBytes;
            if (excess <= 0) break;
        }
    }
}