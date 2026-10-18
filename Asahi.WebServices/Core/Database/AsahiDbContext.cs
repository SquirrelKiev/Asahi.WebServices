using Microsoft.EntityFrameworkCore;

namespace Asahi.WebServices.Database;

public class AsahiDbContext(DbContextOptions<AsahiDbContext> contextOptions) : DbContext(contextOptions)
{
    
}