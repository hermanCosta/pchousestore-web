using Microsoft.EntityFrameworkCore;
using PcHouseStore.Infrastructure.Persistence;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.IntegrationTests;

public sealed class ServiceOrderItemPersistenceTests
{
    [Fact]
    public void ServiceOrderItemModel_MapsFixedLineDiscountAndCompanyScopedRelationships()
    {
        var options = new DbContextOptionsBuilder<PcHouseStoreDbContext>()
            .UseNpgsql("Host=localhost;Database=pchousestore_model_test;Username=test;Password=test")
            .Options;
        using var context = new PcHouseStoreDbContext(options);

        var item = context.Model.FindEntityType(typeof(ServiceOrderItemRecord));
        Assert.NotNull(item);
        Assert.Equal("service_order_item", item!.GetTableName());
        Assert.Equal("numeric(19,2)", item.FindProperty(nameof(ServiceOrderItemRecord.Discount))!.GetColumnType());
        Assert.Equal(0m, item.FindProperty(nameof(ServiceOrderItemRecord.Discount))!.GetDefaultValue());
        Assert.Equal("numeric(19,2)", item.FindProperty(nameof(ServiceOrderItemRecord.LineTotal))!.GetColumnType());
        Assert.Equal("numeric(9,6)", item.FindProperty(nameof(ServiceOrderItemRecord.TaxRateSnapshot))!.GetColumnType());
        Assert.Equal("timestamp with time zone", item.FindProperty(nameof(ServiceOrderItemRecord.RemovedAt))!.GetColumnType());

        Assert.Contains(item.GetCheckConstraints(), c => c.Name == "ck_service_order_item_type_quantity");
        Assert.Contains(item.GetCheckConstraints(), c => c.Name == "ck_service_order_item_amounts");
        Assert.Contains(item.GetCheckConstraints(), c => c.Name == "ck_service_order_item_tax_rate");

        Assert.Contains(item.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(ServiceOrderRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderItemRecord.ServiceOrderId), nameof(ServiceOrderItemRecord.CompanyId) }) &&
            fk.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(item.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(CatalogItemRecord) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ServiceOrderItemRecord.CatalogItemId), nameof(ServiceOrderItemRecord.CompanyId) }) &&
            fk.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.DoesNotContain(context.Model.FindEntityType(typeof(ServiceOrderRecord))!.GetProperties(), p => p.Name == "Discount");
    }
}
