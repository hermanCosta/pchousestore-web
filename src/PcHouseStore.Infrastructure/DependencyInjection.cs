using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PcHouseStore.Infrastructure.Persistence;

namespace PcHouseStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PcHouseStore");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:PcHouseStore is required.");

        services.AddDbContext<PcHouseStoreDbContext>(options =>
            options.UseNpgsql(connectionString).UseOpenIddict());
        return services;
    }
}
