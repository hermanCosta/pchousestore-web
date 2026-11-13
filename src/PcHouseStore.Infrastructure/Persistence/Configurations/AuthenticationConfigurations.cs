using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PcHouseStore.Infrastructure.Persistence.Entities;

namespace PcHouseStore.Infrastructure.Persistence.Configurations;

public sealed class CompanyRecordConfiguration : IEntityTypeConfiguration<CompanyRecord>
{
    public void Configure(EntityTypeBuilder<CompanyRecord> builder)
    {
        builder.ToTable("company");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasColumnType("text").IsRequired();
        builder.Property(x => x.ContactOne).HasColumnName("contact_one").HasColumnType("text").IsRequired();
        builder.Property(x => x.ContactTwo).HasColumnName("contact_two").HasColumnType("text");
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("text");
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).HasDefaultValue("IE").IsRequired();
        builder.Property(x => x.DefaultCurrencyCode).HasColumnName("default_currency_code").HasColumnType("character(3)").HasDefaultValue("EUR").IsRequired();
        builder.Property(x => x.TimeZone).HasColumnName("time_zone").HasMaxLength(80).HasDefaultValue("Europe/Dublin").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
    }
}

public sealed class ShopRecordConfiguration : IEntityTypeConfiguration<ShopRecord>
{
    public void Configure(EntityTypeBuilder<ShopRecord> builder)
    {
        builder.ToTable("shop");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_shop_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(x => x.TimeZone).HasColumnName("time_zone").HasMaxLength(80).HasDefaultValue("Europe/Dublin").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("uq_shop_company_code");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PersonRecordConfiguration : IEntityTypeConfiguration<PersonRecord>
{
    public void Configure(EntityTypeBuilder<PersonRecord> builder)
    {
        builder.ToTable("person");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_person_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.FirstName).HasColumnName("first_name").HasColumnType("text").IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").HasColumnType("text").IsRequired();
        builder.Property(x => x.ContactNo).HasColumnName("contact_no").HasColumnType("text").IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("text");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeRecordConfiguration : IEntityTypeConfiguration<EmployeeRecord>
{
    public void Configure(EntityTypeBuilder<EmployeeRecord> builder)
    {
        builder.ToTable("employee", table => table.HasCheckConstraint("ck_employee_credential_version", "credential_version > 0"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_employee_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PersonId).HasColumnName("person_id");
        builder.Property(x => x.EmployeeCode).HasColumnName("employee_code").HasMaxLength(40).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.MustChangePasswordAtNextUse).HasColumnName("must_change_password_at_next_use").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CredentialVersion).HasColumnName("credential_version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.EmployeeCode }).IsUnique().HasDatabaseName("uq_employee_company_code");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PersonRecord>().WithMany().HasForeignKey(x => new { x.PersonId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeAdministrationEventRecordConfiguration : IEntityTypeConfiguration<EmployeeAdministrationEventRecord>
{
    public void Configure(EntityTypeBuilder<EmployeeAdministrationEventRecord> builder)
    {
        builder.ToTable("employee_administration_event");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ActorEmployeeId).HasColumnName("actor_employee_id");
        builder.Property(x => x.ActorType).HasColumnName("actor_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ActorIdentifier).HasColumnName("actor_identifier").HasMaxLength(240);
        builder.Property(x => x.TargetEmployeeId).HasColumnName("target_employee_id");
        builder.Property(x => x.ActionCode).HasColumnName("action_code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.OccurredAt }).HasDatabaseName("ix_employee_administration_event_company_time");
        builder.HasIndex(x => new { x.TargetEmployeeId, x.OccurredAt }).HasDatabaseName("ix_employee_administration_event_target_time");
        builder.HasOne<CompanyRecord>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.ActorEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.TargetEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeRoleAssignmentRecordConfiguration : IEntityTypeConfiguration<EmployeeRoleAssignmentRecord>
{
    public void Configure(EntityTypeBuilder<EmployeeRoleAssignmentRecord> builder)
    {
        builder.ToTable("employee_role_assignment", table =>
            table.HasCheckConstraint("ck_employee_role_code", "role_code IN ('MANAGER', 'TECHNICIAN')"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CompanyId }).HasName("ak_employee_role_id_company");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.AssignedAt).HasColumnName("assigned_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.AssignedByEmployeeId).HasColumnName("assigned_by_employee_id");
        builder.HasIndex(x => new { x.EmployeeId, x.RoleCode }).IsUnique().HasDatabaseName("uq_employee_role");
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.AssignedByEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShopAccessCredentialRecordConfiguration : IEntityTypeConfiguration<ShopAccessCredentialRecord>
{
    public void Configure(EntityTypeBuilder<ShopAccessCredentialRecord> builder)
    {
        builder.ToTable("shop_access_credential", table =>
            table.HasCheckConstraint("ck_shop_access_credential_version", "credential_version > 0"));
        builder.HasKey(x => x.ShopId);
        builder.Property(x => x.ShopId).HasColumnName("shop_id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        builder.Property(x => x.CredentialVersion).HasColumnName("credential_version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.RotatedAt).HasColumnName("rotated_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.UpdatedByEmployeeId).HasColumnName("updated_by_employee_id");
        builder.HasOne<ShopRecord>().WithOne().HasForeignKey<ShopAccessCredentialRecord>(x => new { x.ShopId, x.CompanyId })
            .HasPrincipalKey<ShopRecord>(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.UpdatedByEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShopSessionRecordConfiguration : IEntityTypeConfiguration<ShopSessionRecord>
{
    public void Configure(EntityTypeBuilder<ShopSessionRecord> builder)
    {
        builder.ToTable("shop_session", table =>
        {
            table.HasCheckConstraint("ck_shop_session_expiry_order", "(idle_expires_at IS NULL OR idle_expires_at > issued_at) AND (absolute_expires_at IS NULL OR absolute_expires_at > issued_at)");
            table.HasCheckConstraint("ck_shop_session_row_version", "row_version > 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.ShopId }).HasName("uq_shop_session_id_shop");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ShopId).HasColumnName("shop_id").IsRequired();
        builder.Property(x => x.OidcAuthorizationId).HasColumnName("oidc_authorization_id").HasMaxLength(100);
        builder.Property(x => x.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.IdleExpiresAt).HasColumnName("idle_expires_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.AbsoluteExpiresAt).HasColumnName("absolute_expires_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(80);
        builder.Property(x => x.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();
        builder.HasIndex(x => new { x.ShopId, x.RevokedAt, x.AbsoluteExpiresAt }).HasDatabaseName("ix_shop_session_active_shop");
        builder.HasOne<ShopRecord>().WithMany().HasForeignKey(x => new { x.ShopId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShopAccessCredentialEventRecordConfiguration : IEntityTypeConfiguration<ShopAccessCredentialEventRecord>
{
    public void Configure(EntityTypeBuilder<ShopAccessCredentialEventRecord> builder)
    {
        builder.ToTable("shop_access_credential_event", table =>
        {
            table.HasCheckConstraint("ck_shop_credential_event_type", "event_type IN ('PROVISIONED', 'ROTATED', 'DISABLED', 'ENABLED', 'RESET')");
            table.HasCheckConstraint("ck_shop_credential_event_actor", "actor_type IN ('EMPLOYEE', 'DEPLOYMENT_OPERATOR')");
            table.HasCheckConstraint("ck_shop_credential_event_version", "credential_version > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("shop_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(24).IsRequired();
        builder.Property(x => x.CredentialVersion).HasColumnName("credential_version").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ActorType).HasColumnName("actor_type").HasMaxLength(24).IsRequired();
        builder.Property(x => x.ActorEmployeeId).HasColumnName("actor_employee_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.ShopId, x.OccurredAt }).HasDatabaseName("ix_shop_credential_event_shop_time");
        builder.HasOne<ShopRecord>().WithMany().HasForeignKey(x => new { x.ShopId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.ActorEmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeActionAuthorizationRecordConfiguration : IEntityTypeConfiguration<EmployeeActionAuthorizationRecord>
{
    public void Configure(EntityTypeBuilder<EmployeeActionAuthorizationRecord> builder)
    {
        builder.ToTable("employee_action_authorization", table =>
        {
            table.HasCheckConstraint("ck_employee_action_expiry", "expires_at > authenticated_at");
            table.HasCheckConstraint("ck_employee_action_row_version", "row_version > 0");
            table.HasCheckConstraint("ck_employee_action_hash_lengths", "octet_length(authorization_token_hash) = 32 AND (operation_context_hash IS NULL OR octet_length(operation_context_hash) = 32)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.AuthorizationTokenHash).HasColumnName("authorization_token_hash").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.ShopSessionId).HasColumnName("shop_session_id").IsRequired();
        builder.Property(x => x.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(x => x.ShopId).HasColumnName("shop_id").IsRequired();
        builder.Property(x => x.TargetShopId).HasColumnName("target_shop_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.ActionCode).HasColumnName("action_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(80);
        builder.Property(x => x.ResourceId).HasColumnName("resource_id").HasMaxLength(100);
        builder.Property(x => x.OperationContextHash).HasColumnName("operation_context_hash").HasColumnType("bytea");
        builder.Property(x => x.AuthenticatedAt).HasColumnName("authenticated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();
        builder.HasIndex(x => x.AuthorizationTokenHash).IsUnique().HasDatabaseName("uq_employee_action_token_hash");
        builder.HasIndex(x => new { x.ShopSessionId, x.ShopId, x.ConsumedAt, x.ExpiresAt }).HasDatabaseName("ix_employee_action_session_state");
        builder.HasIndex(x => new { x.EmployeeId, x.AuthenticatedAt }).HasDatabaseName("ix_employee_action_employee_time");
        builder.HasOne<ShopSessionRecord>().WithMany().HasForeignKey(x => new { x.ShopSessionId, x.ShopId })
            .HasPrincipalKey(x => new { x.Id, x.ShopId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShopRecord>().WithMany().HasForeignKey(x => new { x.ShopId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShopRecord>().WithMany().HasForeignKey(x => new { x.TargetShopId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SystemServiceSigningKeyRecordConfiguration : IEntityTypeConfiguration<SystemServiceSigningKeyRecord>
{
    public void Configure(EntityTypeBuilder<SystemServiceSigningKeyRecord> builder)
    {
        builder.ToTable("system_services", table =>
        {
            table.HasCheckConstraint("ck_system_services_purpose", "key_purpose = 'SIGNING'");
            table.HasCheckConstraint("ck_system_services_status", "status IN ('STAGED', 'ACTIVE', 'VERIFY_ONLY', 'REVOKED')");
            table.HasCheckConstraint("ck_system_services_algorithm", "algorithm = 'RS256'");
            table.HasCheckConstraint("ck_system_services_dates", "not_after > not_before");
            table.HasCheckConstraint("ck_system_services_nonce_tag_lengths", "(cipher_nonce IS NULL OR octet_length(cipher_nonce) = 12) AND (cipher_tag IS NULL OR octet_length(cipher_tag) = 16)");
            table.HasCheckConstraint("ck_system_services_private_material", "(private_key_ciphertext IS NULL AND cipher_nonce IS NULL AND cipher_tag IS NULL AND kek_reference IS NULL) OR (private_key_ciphertext IS NOT NULL AND cipher_nonce IS NOT NULL AND cipher_tag IS NOT NULL AND kek_reference IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ServiceName).HasColumnName("service_name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ActiveSigningSlot).HasColumnName("active_signing_slot")
            .HasMaxLength(80)
            .HasComputedColumnSql("CASE WHEN key_purpose = 'SIGNING' AND status = 'ACTIVE' THEN service_name ELSE NULL END", stored: true);
        builder.Property(x => x.KeyId).HasColumnName("key_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.KeyPurpose).HasColumnName("key_purpose").HasMaxLength(24).IsRequired();
        builder.Property(x => x.Algorithm).HasColumnName("algorithm").HasMaxLength(24).IsRequired();
        builder.Property(x => x.PublicCertificateDer).HasColumnName("public_certificate_der").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.PrivateKeyCiphertext).HasColumnName("private_key_ciphertext").HasColumnType("bytea");
        builder.Property(x => x.CipherNonce).HasColumnName("cipher_nonce").HasColumnType("bytea");
        builder.Property(x => x.CipherTag).HasColumnName("cipher_tag").HasColumnType("bytea");
        builder.Property(x => x.KekReference).HasColumnName("kek_reference").HasMaxLength(255);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.NotBefore).HasColumnName("not_before").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.NotAfter).HasColumnName("not_after").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ActivatedAt).HasColumnName("activated_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RetiredAt).HasColumnName("retired_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.ServiceName, x.KeyId }).IsUnique().HasDatabaseName("uq_system_services_name_key");
        builder.HasIndex(x => x.ActiveSigningSlot).IsUnique().HasDatabaseName("uq_system_services_active_signing_slot");
        builder.HasIndex(x => new { x.ServiceName, x.KeyPurpose, x.Status, x.NotBefore, x.NotAfter }).HasDatabaseName("ix_system_services_use");
    }
}

public sealed class SystemServiceKeyEventRecordConfiguration : IEntityTypeConfiguration<SystemServiceKeyEventRecord>
{
    public void Configure(EntityTypeBuilder<SystemServiceKeyEventRecord> builder)
    {
        builder.ToTable("system_service_key_event", table =>
            table.HasCheckConstraint("ck_system_service_key_event_actor", "actor_type IN ('EMPLOYEE', 'DEPLOYMENT_OPERATOR')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SystemServiceId).HasColumnName("system_service_id").IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(32).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ActorType).HasColumnName("actor_type").HasMaxLength(24).IsRequired();
        builder.Property(x => x.ActorEmployeeId).HasColumnName("actor_employee_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.SystemServiceId, x.OccurredAt }).HasDatabaseName("ix_system_service_key_event_service_time");
        builder.HasOne<SystemServiceSigningKeyRecord>().WithMany().HasForeignKey(x => x.SystemServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeRecord>().WithMany().HasForeignKey(x => x.ActorEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
