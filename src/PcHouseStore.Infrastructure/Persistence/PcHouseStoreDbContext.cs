using Microsoft.EntityFrameworkCore;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Infrastructure.Persistence;

public sealed class PcHouseStoreDbContext(DbContextOptions<PcHouseStoreDbContext> options)
    : DbContext(options)
{
    public DbSet<CompanyRecord> Companies => Set<CompanyRecord>();
    public DbSet<ShopRecord> Shops => Set<ShopRecord>();
    public DbSet<EmployeeRecord> Employees => Set<EmployeeRecord>();
    public DbSet<EmployeeRoleAssignmentRecord> EmployeeRoleAssignments => Set<EmployeeRoleAssignmentRecord>();
    public DbSet<ShopAccessCredentialRecord> ShopAccessCredentials => Set<ShopAccessCredentialRecord>();
    public DbSet<ShopAccessCredentialEventRecord> ShopAccessCredentialEvents => Set<ShopAccessCredentialEventRecord>();
    public DbSet<ShopSessionRecord> ShopSessions => Set<ShopSessionRecord>();
    public DbSet<EmployeeActionAuthorizationRecord> EmployeeActionAuthorizations => Set<EmployeeActionAuthorizationRecord>();
    public DbSet<SystemServiceSigningKeyRecord> SystemServices => Set<SystemServiceSigningKeyRecord>();
    public DbSet<SystemServiceKeyEventRecord> SystemServiceKeyEvents => Set<SystemServiceKeyEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PcHouseStoreDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
