using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "employee",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<long>(
                name: "credential_version",
                table: "employee",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password_at_next_use",
                table: "employee",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "employee",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.Sql("ALTER TABLE employee ALTER COLUMN created_at DROP DEFAULT; ALTER TABLE employee ALTER COLUMN updated_at DROP DEFAULT;");

            migrationBuilder.CreateTable(
                name: "employee_administration_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    actor_employee_id = table.Column<long>(type: "bigint", nullable: false),
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
                        name: "FK_employee_administration_event_employee_actor_employee_id_co~",
                        columns: x => new { x.actor_employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_administration_event_employee_target_employee_id_c~",
                        columns: x => new { x.target_employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_employee_credential_version",
                table: "employee",
                sql: "credential_version > 0");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_administration_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employee_credential_version",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "credential_version",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "must_change_password_at_next_use",
                table: "employee");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "employee");
        }
    }
}
