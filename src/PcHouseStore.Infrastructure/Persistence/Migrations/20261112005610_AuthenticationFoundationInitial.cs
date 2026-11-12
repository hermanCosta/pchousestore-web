using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthenticationFoundationInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    contact_one = table.Column<string>(type: "text", nullable: false),
                    contact_two = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "IE"),
                    default_currency_code = table.Column<string>(type: "character(3)", nullable: false, defaultValue: "EUR"),
                    time_zone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "Europe/Dublin"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_services",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    active_signing_slot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true, computedColumnSql: "CASE WHEN key_purpose = 'SIGNING' AND status = 'ACTIVE' THEN service_name ELSE NULL END", stored: true),
                    key_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    key_purpose = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    algorithm = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    public_certificate_der = table.Column<byte[]>(type: "bytea", nullable: false),
                    private_key_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    cipher_nonce = table.Column<byte[]>(type: "bytea", nullable: true),
                    cipher_tag = table.Column<byte[]>(type: "bytea", nullable: true),
                    kek_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    not_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_services", x => x.id);
                    table.CheckConstraint("ck_system_services_algorithm", "algorithm = 'RS256'");
                    table.CheckConstraint("ck_system_services_dates", "not_after > not_before");
                    table.CheckConstraint("ck_system_services_nonce_tag_lengths", "(cipher_nonce IS NULL OR octet_length(cipher_nonce) = 12) AND (cipher_tag IS NULL OR octet_length(cipher_tag) = 16)");
                    table.CheckConstraint("ck_system_services_private_material", "(private_key_ciphertext IS NULL AND cipher_nonce IS NULL AND cipher_tag IS NULL AND kek_reference IS NULL) OR (private_key_ciphertext IS NOT NULL AND cipher_nonce IS NOT NULL AND cipher_tag IS NOT NULL AND kek_reference IS NOT NULL)");
                    table.CheckConstraint("ck_system_services_purpose", "key_purpose = 'SIGNING'");
                    table.CheckConstraint("ck_system_services_status", "status IN ('STAGED', 'ACTIVE', 'VERIFY_ONLY', 'REVOKED')");
                });

            migrationBuilder.CreateTable(
                name: "person",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    contact_no = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person", x => x.id);
                    table.UniqueConstraint("ak_person_id_company", x => new { x.id, x.company_id });
                    table.ForeignKey(
                        name: "FK_person_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    time_zone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "Europe/Dublin"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop", x => x.id);
                    table.UniqueConstraint("ak_shop_id_company", x => new { x.id, x.company_id });
                    table.ForeignKey(
                        name: "FK_shop_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "employee",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    person_id = table.Column<long>(type: "bigint", nullable: true),
                    employee_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee", x => x.id);
                    table.UniqueConstraint("ak_employee_id_company", x => new { x.id, x.company_id });
                    table.ForeignKey(
                        name: "FK_employee_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_person_person_id_company_id",
                        columns: x => new { x.person_id, x.company_id },
                        principalTable: "person",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_session",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    oidc_authorization_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    idle_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    absolute_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_session", x => x.id);
                    table.UniqueConstraint("uq_shop_session_id_shop", x => new { x.id, x.shop_id });
                    table.CheckConstraint("ck_shop_session_expiry_order", "(idle_expires_at IS NULL OR idle_expires_at > issued_at) AND (absolute_expires_at IS NULL OR absolute_expires_at > issued_at)");
                    table.CheckConstraint("ck_shop_session_row_version", "row_version > 0");
                    table.ForeignKey(
                        name: "FK_shop_session_shop_shop_id_company_id",
                        columns: x => new { x.shop_id, x.company_id },
                        principalTable: "shop",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    AuthorizationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "employee_role_assignment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    role_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_by_employee_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_role_assignment", x => x.id);
                    table.UniqueConstraint("ak_employee_role_id_company", x => new { x.id, x.company_id });
                    table.CheckConstraint("ck_employee_role_code", "role_code IN ('MANAGER', 'TECHNICIAN')");
                    table.ForeignKey(
                        name: "FK_employee_role_assignment_employee_assigned_by_employee_id_c~",
                        columns: x => new { x.assigned_by_employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_role_assignment_employee_employee_id_company_id",
                        columns: x => new { x.employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_access_credential",
                columns: table => new
                {
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    credential_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rotated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_employee_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_access_credential", x => x.shop_id);
                    table.CheckConstraint("ck_shop_access_credential_version", "credential_version > 0");
                    table.ForeignKey(
                        name: "FK_shop_access_credential_employee_updated_by_employee_id_comp~",
                        columns: x => new { x.updated_by_employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shop_access_credential_shop_shop_id_company_id",
                        columns: x => new { x.shop_id, x.company_id },
                        principalTable: "shop",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_access_credential_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    credential_version = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    actor_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_access_credential_event", x => x.id);
                    table.CheckConstraint("ck_shop_credential_event_actor", "actor_type IN ('EMPLOYEE', 'DEPLOYMENT_OPERATOR')");
                    table.CheckConstraint("ck_shop_credential_event_type", "event_type IN ('PROVISIONED', 'ROTATED', 'DISABLED', 'ENABLED', 'RESET')");
                    table.CheckConstraint("ck_shop_credential_event_version", "credential_version > 0");
                    table.ForeignKey(
                        name: "FK_shop_access_credential_event_employee_actor_employee_id_com~",
                        columns: x => new { x.actor_employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shop_access_credential_event_shop_shop_id_company_id",
                        columns: x => new { x.shop_id, x.company_id },
                        principalTable: "shop",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_service_key_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    system_service_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    actor_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_service_key_event", x => x.id);
                    table.CheckConstraint("ck_system_service_key_event_actor", "actor_type IN ('EMPLOYEE', 'DEPLOYMENT_OPERATOR')");
                    table.ForeignKey(
                        name: "FK_system_service_key_event_employee_actor_employee_id",
                        column: x => x.actor_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_system_service_key_event_system_services_system_service_id",
                        column: x => x.system_service_id,
                        principalTable: "system_services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_action_authorization",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    authorization_token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    shop_session_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    target_shop_id = table.Column<long>(type: "bigint", nullable: true),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    action_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    resource_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    operation_context_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    authenticated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_action_authorization", x => x.id);
                    table.CheckConstraint("ck_employee_action_expiry", "expires_at > authenticated_at");
                    table.CheckConstraint("ck_employee_action_hash_lengths", "octet_length(authorization_token_hash) = 32 AND (operation_context_hash IS NULL OR octet_length(operation_context_hash) = 32)");
                    table.CheckConstraint("ck_employee_action_row_version", "row_version > 0");
                    table.ForeignKey(
                        name: "FK_employee_action_authorization_employee_employee_id_company_~",
                        columns: x => new { x.employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_action_authorization_shop_session_shop_session_id_~",
                        columns: x => new { x.shop_session_id, x.shop_id },
                        principalTable: "shop_session",
                        principalColumns: new[] { "id", "shop_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_action_authorization_shop_shop_id_company_id",
                        columns: x => new { x.shop_id, x.company_id },
                        principalTable: "shop",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_action_authorization_shop_target_shop_id_company_id",
                        columns: x => new { x.target_shop_id, x.company_id },
                        principalTable: "shop",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_person_id_company_id",
                table: "employee",
                columns: new[] { "person_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_employee_company_code",
                table: "employee",
                columns: new[] { "company_id", "employee_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_action_authorization_employee_id_company_id",
                table: "employee_action_authorization",
                columns: new[] { "employee_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_action_authorization_shop_id_company_id",
                table: "employee_action_authorization",
                columns: new[] { "shop_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_action_authorization_target_shop_id_company_id",
                table: "employee_action_authorization",
                columns: new[] { "target_shop_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_action_employee_time",
                table: "employee_action_authorization",
                columns: new[] { "employee_id", "authenticated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_action_session_state",
                table: "employee_action_authorization",
                columns: new[] { "shop_session_id", "shop_id", "consumed_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_employee_action_token_hash",
                table: "employee_action_authorization",
                column: "authorization_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_role_assignment_assigned_by_employee_id_company_id",
                table: "employee_role_assignment",
                columns: new[] { "assigned_by_employee_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_role_assignment_employee_id_company_id",
                table: "employee_role_assignment",
                columns: new[] { "employee_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_employee_role",
                table: "employee_role_assignment",
                columns: new[] { "employee_id", "role_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                table: "OpenIddictTokens",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_company_id",
                table: "person",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_shop_company_code",
                table: "shop",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_access_credential_shop_id_company_id",
                table: "shop_access_credential",
                columns: new[] { "shop_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_access_credential_updated_by_employee_id_company_id",
                table: "shop_access_credential",
                columns: new[] { "updated_by_employee_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_access_credential_event_actor_employee_id_company_id",
                table: "shop_access_credential_event",
                columns: new[] { "actor_employee_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_access_credential_event_shop_id_company_id",
                table: "shop_access_credential_event",
                columns: new[] { "shop_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_credential_event_shop_time",
                table: "shop_access_credential_event",
                columns: new[] { "shop_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_session_active_shop",
                table: "shop_session",
                columns: new[] { "shop_id", "revoked_at", "absolute_expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_session_shop_id_company_id",
                table: "shop_session",
                columns: new[] { "shop_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "IX_system_service_key_event_actor_employee_id",
                table: "system_service_key_event",
                column: "actor_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_service_key_event_service_time",
                table: "system_service_key_event",
                columns: new[] { "system_service_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_system_services_use",
                table: "system_services",
                columns: new[] { "service_name", "key_purpose", "status", "not_before", "not_after" });

            migrationBuilder.CreateIndex(
                name: "uq_system_services_active_signing_slot",
                table: "system_services",
                column: "active_signing_slot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_system_services_name_key",
                table: "system_services",
                columns: new[] { "service_name", "key_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_action_authorization");

            migrationBuilder.DropTable(
                name: "employee_role_assignment");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "shop_access_credential");

            migrationBuilder.DropTable(
                name: "shop_access_credential_event");

            migrationBuilder.DropTable(
                name: "system_service_key_event");

            migrationBuilder.DropTable(
                name: "shop_session");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "employee");

            migrationBuilder.DropTable(
                name: "system_services");

            migrationBuilder.DropTable(
                name: "shop");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications");

            migrationBuilder.DropTable(
                name: "person");

            migrationBuilder.DropTable(
                name: "company");
        }
    }
}
