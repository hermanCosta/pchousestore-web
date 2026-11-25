using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;
using PcHouseStore.Infrastructure.Persistence.Migrations;

namespace PcHouseStore.IntegrationTests;

public sealed class ServiceOrderPersistenceTests
{
    [Fact]
    public void ServiceOrderModel_MapsApprovedCompanyScopedShape()
    {
        var options = new DbContextOptionsBuilder<PcHouseStoreDbContext>()
            .UseNpgsql("Host=localhost;Database=pchousestore_model_test;Username=test;Password=test")
            .Options;
        using var context = new PcHouseStoreDbContext(options);
        var entity = context.Model.FindEntityType(typeof(ServiceOrderRecord));

        Assert.NotNull(entity);
        Assert.Equal("service_order", entity!.GetTableName());
        Assert.Equal("numeric(19,2)", entity.FindProperty(nameof(ServiceOrderRecord.Total))!.GetColumnType());
        Assert.Equal("timestamp with time zone", entity.FindProperty(nameof(ServiceOrderRecord.CreatedAt))!.GetColumnType());
        Assert.Equal("CREATED", entity.FindProperty(nameof(ServiceOrderRecord.Status))!.GetDefaultValue());
        Assert.Equal("EUR", entity.FindProperty(nameof(ServiceOrderRecord.CurrencyCode))!.GetDefaultValue());
        Assert.Null(entity.FindProperty(nameof(ServiceOrderRecord.TechnicalOutcome))!.GetDefaultValue());
        Assert.Null(entity.FindProperty(nameof(ServiceOrderRecord.PickedAt))!.GetDefaultValue());

        var alternateKey = Assert.Single(entity.GetKeys(), key =>
            key.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(ServiceOrderRecord.Id), nameof(ServiceOrderRecord.CompanyId) }));
        Assert.Equal("ak_service_order_id_company", alternateKey.GetName());

        Assert.Single(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(CompanyRecord));
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(ShopRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderRecord.OriginShopId), nameof(ServiceOrderRecord.CompanyId) }) &&
            fk.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(ShopRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderRecord.ShopId), nameof(ServiceOrderRecord.CompanyId) }) &&
            fk.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(CustomerRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderRecord.CustomerId), nameof(ServiceOrderRecord.CompanyId) }));
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(DeviceRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderRecord.DeviceId), nameof(ServiceOrderRecord.CompanyId) }));
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(EmployeeRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderRecord.CreatedByEmployeeId), nameof(ServiceOrderRecord.CompanyId) }));
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(EmployeeRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderRecord.ResponsibleEmployeeId), nameof(ServiceOrderRecord.CompanyId) }));

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderRecord.ShopId), nameof(ServiceOrderRecord.DisplayNumber) }));
        Assert.DoesNotContain(entity.GetProperties(), p => p.Name.Contains("CustomerName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddServiceOrderFoundationMigration_CreatesApprovedConstraints()
    {
        var migration = new AddServiceOrderFoundation();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var up = typeof(AddServiceOrderFoundation).GetMethod(
            "Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(up);
        up!.Invoke(migration, [builder]);

        var table = Assert.Single(builder.Operations.OfType<CreateTableOperation>());
        Assert.Equal("service_order", table.Name);
        Assert.Contains(table.Columns, c => c.Name == "status" && Equals(c.DefaultValue, "CREATED"));
        Assert.Contains(table.Columns, c => c.Name == "technical_outcome" && c.IsNullable);
        Assert.Contains(table.Columns, c => c.Name == "picked_at" && c.IsNullable);
        Assert.Contains(table.Columns, c => c.Name == "bad_sector" && c.IsNullable);
        Assert.Contains(table.UniqueConstraints, c =>
            c.Name == "ak_service_order_id_company" &&
            c.Columns.SequenceEqual(new[] { "id", "company_id" }));
        Assert.Equal(7, table.ForeignKeys.Count);
        Assert.Contains(table.ForeignKeys, fk =>
            fk.Columns.SequenceEqual(new[] { "origin_shop_id", "company_id" }) &&
            fk.PrincipalTable == "shop" && fk.OnDelete == ReferentialAction.Restrict);
        Assert.Contains(table.ForeignKeys, fk =>
            fk.Columns.SequenceEqual(new[] { "shop_id", "company_id" }) &&
            fk.PrincipalTable == "shop" && fk.OnDelete == ReferentialAction.Restrict);
        Assert.Contains(table.ForeignKeys, fk =>
            fk.Columns.SequenceEqual(new[] { "customer_id", "company_id" }) &&
            fk.PrincipalTable == "customer");
        Assert.Contains(table.ForeignKeys, fk =>
            fk.Columns.SequenceEqual(new[] { "device_id", "company_id" }) &&
            fk.PrincipalTable == "device");
        Assert.Contains(table.Columns, c => c.Name == "refurbished_unit_warranty_id" && c.IsNullable);
        Assert.DoesNotContain(table.Columns, c => c.Name == "customer_name");

        var uniqueIndex = Assert.Single(builder.Operations.OfType<CreateIndexOperation>(), i => i.IsUnique);
        Assert.Equal("uq_service_order_shop_display_number", uniqueIndex.Name);
        Assert.Equal(new[] { "shop_id", "display_number" }, uniqueIndex.Columns);

        var downBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var down = typeof(AddServiceOrderFoundation).GetMethod(
            "Down", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(down);
        down!.Invoke(migration, [downBuilder]);
        Assert.Contains(downBuilder.Operations, operation =>
            operation is DropTableOperation drop && drop.Name == "service_order");
    }
}
