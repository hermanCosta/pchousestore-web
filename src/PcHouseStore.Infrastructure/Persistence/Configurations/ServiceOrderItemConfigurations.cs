using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Infrastructure.Persistence.Configurations;

public sealed class ServiceOrderItemRecordConfiguration : IEntityTypeConfiguration<ServiceOrderItemRecord>
{
    public void Configure(EntityTypeBuilder<ServiceOrderItemRecord> builder)
    {
        builder.ToTable("service_order_item", table =>
        {
            table.HasCheckConstraint(
                "ck_service_order_item_type_quantity",
                "(item_type = 'PRODUCT' AND quantity > 0) OR (item_type = 'SERVICE' AND quantity = 1)");
            table.HasCheckConstraint(
                "ck_service_order_item_amounts",
                "unit_price >= 0 AND discount >= 0 AND discount <= quantity * unit_price AND taxable_amount >= 0 AND tax_amount >= 0 AND line_total >= 0 AND line_total = quantity * unit_price - discount");
            table.HasCheckConstraint(
                "ck_service_order_item_tax_rate",
                "(tax_code_snapshot IS NULL AND tax_rate_snapshot IS NULL) OR (tax_code_snapshot IS NOT NULL AND tax_rate_snapshot IS NOT NULL AND tax_rate_snapshot >= 0)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ServiceOrderId).HasColumnName("service_order_id").IsRequired();
        builder.Property(x => x.CatalogItemId).HasColumnName("catalog_item_id").IsRequired();
        builder.Property(x => x.ItemType).HasColumnName("item_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.NameSnapshot).HasColumnName("name_snapshot").HasColumnType("text").IsRequired();
        builder.Property(x => x.DescriptionSnapshot).HasColumnName("description_snapshot").HasColumnType("text");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("integer").IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.Discount).HasColumnName("discount").HasColumnType("numeric(19,2)").HasDefaultValue(0m).IsRequired();
        builder.Property(x => x.TaxableAmount).HasColumnName("taxable_amount").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.TaxCodeSnapshot).HasColumnName("tax_code_snapshot").HasColumnType("text");
        builder.Property(x => x.TaxRateSnapshot).HasColumnName("tax_rate_snapshot").HasColumnType("numeric(9,6)");
        builder.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.LineTotal).HasColumnName("line_total").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.RemovedAt).HasColumnName("removed_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.ServiceOrderId, x.CompanyId })
            .HasDatabaseName("ix_service_order_item_order_company");
        builder.HasIndex(x => new { x.CatalogItemId, x.CompanyId })
            .HasDatabaseName("ix_service_order_item_catalog_company");

        builder.HasOne<ServiceOrderRecord>().WithMany()
            .HasForeignKey(x => new { x.ServiceOrderId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CatalogItemRecord>().WithMany()
            .HasForeignKey(x => new { x.CatalogItemId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
