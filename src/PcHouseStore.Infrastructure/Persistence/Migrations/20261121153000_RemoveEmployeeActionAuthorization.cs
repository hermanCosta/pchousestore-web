using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PcHouseStore.Infrastructure.Persistence;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations;

[Migration("20261121153000_RemoveEmployeeActionAuthorization")]
public partial class RemoveEmployeeActionAuthorization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_employee_role_code",
            table: "employee_role_assignment");

        // Preserve the approved single-profile model: MANAGER takes precedence over the retired TECHNICIAN profile.
        migrationBuilder.Sql("""
            UPDATE employee_role_assignment AS technician
            SET is_active = FALSE
            WHERE technician.role_code = 'TECHNICIAN'
              AND technician.is_active = TRUE
              AND EXISTS (
                  SELECT 1
                  FROM employee_role_assignment AS manager
                  WHERE manager.employee_id = technician.employee_id
                    AND manager.company_id = technician.company_id
                    AND manager.role_code = 'MANAGER'
                    AND manager.is_active = TRUE
              );

            UPDATE employee_role_assignment
            SET role_code = 'EMPLOYEE'
            WHERE role_code = 'TECHNICIAN';
            """);

        migrationBuilder.AddCheckConstraint(
            name: "ck_employee_role_code",
            table: "employee_role_assignment",
            sql: "role_code IN ('EMPLOYEE', 'MANAGER', 'ADMIN')");

        // One-use authorization tokens are ephemeral credentials, not business history.
        // Existing unconsumed records are intentionally invalidated by dropping this table.
        migrationBuilder.CreateIndex(
            name: "uq_employee_one_active_profile",
            table: "employee_role_assignment",
            column: "employee_id",
            unique: true,
            filter: "is_active = TRUE");

        migrationBuilder.DropTable(name: "employee_action_authorization");

        // Employee-administration audit was explicitly excluded from the approved feature scope.
        migrationBuilder.DropTable(name: "employee_administration_event");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "uq_employee_one_active_profile",
            table: "employee_role_assignment");

        migrationBuilder.DropCheckConstraint(
            name: "ck_employee_role_code",
            table: "employee_role_assignment");

        // Downgrade compatibility only: ADMIN is mapped to MANAGER and EMPLOYEE to TECHNICIAN.
        // This mapping is lossy and must not be used as a production rollback strategy.
        migrationBuilder.Sql("""
            DELETE FROM employee_role_assignment admin_role
            USING employee_role_assignment manager_role
            WHERE admin_role.employee_id = manager_role.employee_id
              AND admin_role.company_id = manager_role.company_id
              AND admin_role.role_code = 'ADMIN'
              AND manager_role.role_code = 'MANAGER';

            UPDATE employee_role_assignment SET role_code = 'MANAGER' WHERE role_code = 'ADMIN';
            UPDATE employee_role_assignment SET role_code = 'TECHNICIAN' WHERE role_code = 'EMPLOYEE';
            """);

        migrationBuilder.AddCheckConstraint(
            name: "ck_employee_role_code",
            table: "employee_role_assignment",
            sql: "role_code IN ('MANAGER', 'TECHNICIAN')");

        migrationBuilder.CreateTable(
            name: "employee_administration_event",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                company_id = table.Column<long>(type: "bigint", nullable: false),
                actor_employee_id = table.Column<long>(type: "bigint", nullable: true),
                actor_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                actor_identifier = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                target_employee_id = table.Column<long>(type: "bigint", nullable: true),
                action_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_employee_administration_event", x => x.id);
                table.ForeignKey(
                    name: "FK_employee_administration_event_company_company_id",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_employee_administration_event_employee_actor_employee_id_company_id",
                    columns: x => new { x.actor_employee_id, x.company_id },
                    principalTable: "employee",
                    principalColumns: new[] { "id", "company_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_employee_administration_event_employee_target_employee_id_company_id",
                    columns: x => new { x.target_employee_id, x.company_id },
                    principalTable: "employee",
                    principalColumns: new[] { "id", "company_id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_employee_administration_event_actor_employee_id_company_id",
            table: "employee_administration_event",
            columns: new[] { "actor_employee_id", "company_id" });

        migrationBuilder.CreateIndex(
            name: "ix_employee_administration_event_company_time",
            table: "employee_administration_event",
            columns: new[] { "company_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "IX_employee_administration_event_target_employee_id_company_id",
            table: "employee_administration_event",
            columns: new[] { "target_employee_id", "company_id" });

        migrationBuilder.CreateIndex(
            name: "ix_employee_administration_event_target_time",
            table: "employee_administration_event",
            columns: new[] { "target_employee_id", "occurred_at" });

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
                    name: "fk_employee_action_employee_company",
                    columns: x => new { x.employee_id, x.company_id },
                    principalTable: "employee",
                    principalColumns: new[] { "id", "company_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_employee_action_session_shop",
                    columns: x => new { x.shop_session_id, x.shop_id },
                    principalTable: "shop_session",
                    principalColumns: new[] { "id", "shop_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_employee_action_shop_company",
                    columns: x => new { x.shop_id, x.company_id },
                    principalTable: "shop",
                    principalColumns: new[] { "id", "company_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_employee_action_target_shop_company",
                    columns: x => new { x.target_shop_id, x.company_id },
                    principalTable: "shop",
                    principalColumns: new[] { "id", "company_id" },
                    onDelete: ReferentialAction.Restrict);
            });

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
            name: "ix_employee_action_shop_company",
            table: "employee_action_authorization",
            columns: new[] { "shop_id", "company_id" });

        migrationBuilder.CreateIndex(
            name: "ix_employee_action_target_shop_company",
            table: "employee_action_authorization",
            columns: new[] { "target_shop_id", "company_id" });
    }
}
