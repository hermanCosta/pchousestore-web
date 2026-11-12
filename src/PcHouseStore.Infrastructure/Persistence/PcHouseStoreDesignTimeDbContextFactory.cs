using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PcHouseStore.Infrastructure.Persistence;

public sealed class PcHouseStoreDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<PcHouseStoreDbContext>
{
    public PcHouseStoreDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PcHouseStore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__PcHouseStore in the environment before using EF Core design-time commands.");
        }

        var options = new DbContextOptionsBuilder<PcHouseStoreDbContext>()
            .UseNpgsql(connectionString)
            .UseOpenIddict()
            .Options;

        return new PcHouseStoreDbContext(options);
    }
}
