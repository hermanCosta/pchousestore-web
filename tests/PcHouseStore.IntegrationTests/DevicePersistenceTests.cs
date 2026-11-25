using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;
using PcHouseStore.Infrastructure.Persistence.Migrations;

namespace PcHouseStore.IntegrationTests;

public sealed class DevicePersistenceTests
{
    [Fact]
    public void DeviceModel_MapsApprovedCompanyScopedShape()
    {
        var options = new DbContextOptionsBuilder<PcHouseStoreDbContext>()
            .UseNpgsql("Host=localhost;Database=pchousestore_model_test;Username=test;Password=test")
            .Options;

        using var context = new PcHouseStoreDbContext(options);
        var entity = context.Model.FindEntityType(typeof(DeviceRecord));

        Assert.NotNull(entity);
        Assert.Equal("device", entity!.GetTableName());
        Assert.Equal("id", entity.FindProperty(nameof(DeviceRecord.Id))!.GetColumnName());
        Assert.Equal("company_id", entity.FindProperty(nameof(DeviceRecord.CompanyId))!.GetColumnName());
        Assert.Equal("brand", entity.FindProperty(nameof(DeviceRecord.Brand))!.GetColumnName());
        Assert.Equal("model", entity.FindProperty(nameof(DeviceRecord.Model))!.GetColumnName());
        Assert.Equal("serial_number", entity.FindProperty(nameof(DeviceRecord.SerialNumber))!.GetColumnName());
        Assert.Equal("is_active", entity.FindProperty(nameof(DeviceRecord.IsActive))!.GetColumnName());
        Assert.Equal(true, entity.FindProperty(nameof(DeviceRecord.IsActive))!.GetDefaultValue());

        var alternateKey = Assert.Single(entity.GetKeys(), key =>
            key.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DeviceRecord.Id), nameof(DeviceRecord.CompanyId)]));
        Assert.Equal("ak_device_id_company", alternateKey.GetName());

        var companyForeignKey = Assert.Single(entity.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(CompanyRecord));
        Assert.Equal([nameof(DeviceRecord.CompanyId)],
            companyForeignKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, companyForeignKey.DeleteBehavior);

        Assert.DoesNotContain(entity.GetProperties(), property =>
            property.Name.Contains("Customer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Any(property => property.Name == nameof(DeviceRecord.SerialNumber)));
    }

    [Fact]
    public void AddDeviceFoundationMigration_CreatesOnlyApprovedDeviceConstraints()
    {
        var migration = new AddDeviceFoundation();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var up = typeof(AddDeviceFoundation).GetMethod(
            "Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(up);
        up!.Invoke(migration, [builder]);

        var table = Assert.Single(builder.Operations.OfType<CreateTableOperation>());
        Assert.Equal("device", table.Name);
        Assert.Contains(table.Columns, column => column.Name == "is_active" && Equals(column.DefaultValue, true));
        Assert.Contains(table.UniqueConstraints, constraint =>
            constraint.Name == "ak_device_id_company" &&
            constraint.Columns.SequenceEqual(["id", "company_id"]));
        Assert.Contains(table.ForeignKeyConstraints, foreignKey =>
            foreignKey.Name == "FK_device_company_company_id" &&
            foreignKey.PrincipalTable == "company" &&
            foreignKey.OnDelete == ReferentialAction.Restrict);
        Assert.DoesNotContain(table.Columns, column => column.Name == "customer_id");
        Assert.DoesNotContain(builder.Operations.OfType<CreateIndexOperation>(), index =>
            index.IsUnique && index.Columns.Contains("serial_number", StringComparer.Ordinal));

        var downBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var down = typeof(AddDeviceFoundation).GetMethod(
            "Down", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(down);
        down!.Invoke(migration, [downBuilder]);
        Assert.Contains(downBuilder.Operations, operation =>
            operation is DropTableOperation drop && drop.Name == "device");
    }
}
