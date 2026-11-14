using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Infrastructure.Persistence.Configurations;

public sealed class CustomerRecordConfiguration : IEntityTypeConfiguration<CustomerRecord>
{
    public void Configure(EntityTypeBuilder<CustomerRecord> builder)
    {
        builder.ToTable("customer", table => table.HasCheckConstraint("ck_customer_phone_e164", "phone_e164 ~ '^\\+[1-9][0-9]{7,14}$'"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_customer_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PersonId).HasColumnName("person_id").IsRequired();
        builder.Property(x => x.PhoneE164).HasColumnName("phone_e164").HasMaxLength(16).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.PhoneE164 }).IsUnique().HasDatabaseName("uq_customer_company_phone_e164");
        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Id }).HasDatabaseName("ix_customer_company_active");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PersonRecord>().WithMany().HasForeignKey(x => new { x.PersonId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductCategoryRecordConfiguration : IEntityTypeConfiguration<ProductCategoryRecord>
{
    public void Configure(EntityTypeBuilder<ProductCategoryRecord> builder)
    {
        builder.ToTable("product_category");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_product_category_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ParentCategoryId).HasColumnName("parent_category_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("uq_product_category_company_code");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductCategoryRecord>().WithMany().HasForeignKey(x => new { x.ParentCategoryId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CatalogItemRecordConfiguration : IEntityTypeConfiguration<CatalogItemRecord>
{
    public void Configure(EntityTypeBuilder<CatalogItemRecord> builder)
    {
        builder.ToTable("catalog_item", table =>
        {
            table.HasCheckConstraint("ck_catalog_item_kind", "kind IN ('PRODUCT', 'SERVICE')");
            table.HasCheckConstraint("ck_catalog_item_price", "list_price >= 0");
            table.HasCheckConstraint("ck_catalog_item_sku_format", "sku IS NULL OR (sku = upper(sku) AND sku ~ '^SKU-[A-Z0-9]{8}$')");
            table.HasCheckConstraint("ck_catalog_item_subtype", "(kind = 'PRODUCT' AND product_category_id IS NOT NULL AND inventory_tracking_mode IN ('QUANTITY', 'SERIALIZED_UNIT') AND service_description IS NULL) OR (kind = 'SERVICE' AND product_category_id IS NULL AND inventory_tracking_mode IS NULL AND service_description IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_catalog_item_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(12);
        builder.Property(x => x.ListPrice).HasColumnName("list_price").HasPrecision(19, 2).IsRequired();
        builder.Property(x => x.ProductCategoryId).HasColumnName("product_category_id");
        builder.Property(x => x.InventoryTrackingMode).HasColumnName("inventory_tracking_mode").HasMaxLength(24);
        builder.Property(x => x.ServiceDescription).HasColumnName("service_description").HasColumnType("text");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.Sku }).IsUnique().HasFilter("sku IS NOT NULL").HasDatabaseName("uq_catalog_item_company_sku");
        builder.HasIndex(x => new { x.CompanyId, x.Kind, x.IsActive, x.Name }).HasDatabaseName("ix_catalog_item_company_kind_active_name");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductCategoryRecord>().WithMany().HasForeignKey(x => new { x.ProductCategoryId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FaultRecordConfiguration : IEntityTypeConfiguration<FaultRecord>
{
    public void Configure(EntityTypeBuilder<FaultRecord> builder)
    {
        builder.ToTable("fault", table => table.HasCheckConstraint("ck_fault_sequence_number", "sequence_number > 0"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_fault_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.SequenceNumber).HasColumnName("sequence_number").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(255).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("uq_fault_company_code");
        builder.HasIndex(x => new { x.CompanyId, x.SequenceNumber }).IsUnique().HasDatabaseName("uq_fault_company_sequence");
        builder.HasIndex(x => new { x.CompanyId, x.IsActive, x.Description }).HasDatabaseName("ix_fault_company_active_description");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CatalogueAuditEventRecordConfiguration : IEntityTypeConfiguration<CatalogueAuditEventRecord>
{
    public void Configure(EntityTypeBuilder<CatalogueAuditEventRecord> builder)
    {
        builder.ToTable("catalogue_audit_event", table =>
            table.HasCheckConstraint("ck_catalogue_audit_event_actor", "(actor_type = 'EMPLOYEE' AND actor_employee_id IS NOT NULL AND shop_session_id IS NULL) OR (actor_type = 'SHOP_SESSION' AND actor_employee_id IS NULL AND shop_session_id IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ShopId).HasColumnName("shop_id").IsRequired();
        builder.Property(x => x.ShopSessionId).HasColumnName("shop_session_id");
        builder.Property(x => x.ActorEmployeeId).HasColumnName("actor_employee_id");
        builder.Property(x => x.ActorType).HasColumnName("actor_type").HasMaxLength(20).HasDefaultValue("EMPLOYEE").IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("resource_id").IsRequired();
        builder.Property(x => x.ActionCode).HasColumnName("action_code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.OccurredAt }).HasDatabaseName("ix_catalogue_audit_event_company_time");
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId, x.OccurredAt }).HasDatabaseName("ix_catalogue_audit_event_resource_time");
        builder.HasIndex(x => new { x.ShopSessionId, x.OccurredAt }).HasDatabaseName("ix_catalogue_audit_event_shop_session_time");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShopRecord>().WithMany().HasForeignKey(x => new { x.ShopId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.ActorEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShopSessionRecord>().WithMany().HasForeignKey(x => new { x.ShopSessionId, x.ShopId })
            .HasPrincipalKey(x => new { x.Id, x.ShopId }).OnDelete(DeleteBehavior.Restrict);
    }
}
