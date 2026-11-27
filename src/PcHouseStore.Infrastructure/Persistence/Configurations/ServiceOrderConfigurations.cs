using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Infrastructure.Persistence.Configurations;

public sealed class ServiceOrderRecordConfiguration : IEntityTypeConfiguration<ServiceOrderRecord>
{
    public void Configure(EntityTypeBuilder<ServiceOrderRecord> builder)
    {
        builder.ToTable("service_order", table =>
        {
            table.HasCheckConstraint(
                "ck_service_order_display_number",
                "display_number > 0");
            table.HasCheckConstraint(
                "ck_service_order_status",
                "status IN ('CREATED', 'IN_PROGRESS', 'AWAITING_PARTS', 'FIXED', 'NOT_FIXED', 'PICKED', 'CANCELLED')");
            table.HasCheckConstraint(
                "ck_service_order_technical_outcome",
                "technical_outcome IS NULL OR technical_outcome IN ('FIXED', 'NOT_FIXED')");
            table.HasCheckConstraint(
                "ck_service_order_amounts_non_negative",
                "subtotal >= 0 AND tax_total >= 0 AND total >= 0 AND balance_due >= 0");
            table.HasCheckConstraint(
                "ck_service_order_bad_sector",
                "bad_sector IS NULL OR bad_sector >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_service_order_id_company");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.OriginShopId).HasColumnName("origin_shop_id").IsRequired();
        builder.Property(x => x.ShopId).HasColumnName("shop_id").IsRequired();
        builder.Property(x => x.DisplayNumber).HasColumnName("display_number").IsRequired();
        builder.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(x => x.DeviceId).HasColumnName("device_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ResponsibleEmployeeId).HasColumnName("responsible_employee_id");
        builder.Property(x => x.CreatedByEmployeeId).HasColumnName("created_by_employee_id").IsRequired();
        builder.Property(x => x.RefurbishedUnitWarrantyId).HasColumnName("refurbished_unit_warranty_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).HasDefaultValue("CREATED").IsRequired();
        builder.Property(x => x.TechnicalOutcome).HasColumnName("technical_outcome").HasMaxLength(16);
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("character(3)").HasDefaultValue("EUR").IsRequired();
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.TaxTotal).HasColumnName("tax_total").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.BalanceDue).HasColumnName("balance_due").HasColumnType("numeric(19,2)").IsRequired();
        builder.Property(x => x.PickedAt).HasColumnName("picked_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.BadSector).HasColumnName("bad_sector").HasColumnType("integer");

        builder.HasIndex(x => new { x.ShopId, x.DisplayNumber })
            .IsUnique()
            .HasDatabaseName("uq_service_order_shop_display_number");

        builder.HasOne<CompanyRecord>().WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ShopRecord>().WithMany()
            .HasForeignKey(x => new { x.OriginShopId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ShopRecord>().WithMany()
            .HasForeignKey(x => new { x.ShopId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CustomerRecord>().WithMany()
            .HasForeignKey(x => new { x.CustomerId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DeviceRecord>().WithMany()
            .HasForeignKey(x => new { x.DeviceId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EmployeeRecord>().WithMany()
            .HasForeignKey(x => new { x.CreatedByEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EmployeeRecord>().WithMany()
            .HasForeignKey(x => new { x.ResponsibleEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
