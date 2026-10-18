using Asahi.WebServices.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Asahi.WebServices.Database;

/// <summary>
/// The database context.
/// </summary>
public class AsahiDbContext(DbContextOptions<AsahiDbContext> contextOptions) : DbContext(contextOptions)
{
    /// <summary>
    /// The cached themes table.
    /// </summary>
    public DbSet<CachedTheme> CachedThemes { get; set; }
}