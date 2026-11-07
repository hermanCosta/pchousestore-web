using Microsoft.EntityFrameworkCore;

namespace PcHouseStore.Infrastructure.Persistence;

public sealed class PcHouseStoreDbContext(DbContextOptions<PcHouseStoreDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Entity configurations will be added by their owning modules.
        base.OnModelCreating(modelBuilder);
    }
}
