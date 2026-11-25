using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Infrastructure.Persistence.Configurations;

public sealed class DeviceRecordConfiguration : IEntityTypeConfiguration<DeviceRecord>
{
    public void Configure(EntityTypeBuilder<DeviceRecord> builder)
    {
        builder.ToTable("device");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_device_id_company");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Brand).HasColumnName("brand").HasColumnType("text").IsRequired();
        builder.Property(x => x.Model).HasColumnName("model").HasColumnType("text").IsRequired();
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasColumnType("text").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
