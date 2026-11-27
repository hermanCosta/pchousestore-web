using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogueShopSessionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "actor_employee_id",
                table: "catalogue_audit_event",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "actor_type",
                table: "catalogue_audit_event",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "EMPLOYEE");

            migrationBuilder.AddColumn<long>(
                name: "shop_session_id",
                table: "catalogue_audit_event",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_audit_event_shop_session_id_shop_id",
                table: "catalogue_audit_event",
                columns: new[] { "shop_session_id", "shop_id" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_audit_event_shop_session_time",
                table: "catalogue_audit_event",
                columns: new[] { "shop_session_id", "occurred_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalogue_audit_event_actor",
                table: "catalogue_audit_event",
                sql: "(actor_type = 'EMPLOYEE' AND actor_employee_id IS NOT NULL AND shop_session_id IS NULL) OR (actor_type = 'SHOP_SESSION' AND actor_employee_id IS NULL AND shop_session_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_catalogue_audit_event_shop_session_shop_session_id_shop_id",
                table: "catalogue_audit_event",
                columns: new[] { "shop_session_id", "shop_id" },
                principalTable: "shop_session",
                principalColumns: new[] { "id", "shop_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_catalogue_audit_event_shop_session_shop_session_id_shop_id",
                table: "catalogue_audit_event");

            migrationBuilder.DropIndex(
                name: "IX_catalogue_audit_event_shop_session_id_shop_id",
                table: "catalogue_audit_event");

            migrationBuilder.DropIndex(
                name: "ix_catalogue_audit_event_shop_session_time",
                table: "catalogue_audit_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalogue_audit_event_actor",
                table: "catalogue_audit_event");

            migrationBuilder.DropColumn(
                name: "actor_type",
                table: "catalogue_audit_event");

            migrationBuilder.DropColumn(
                name: "shop_session_id",
                table: "catalogue_audit_event");

            migrationBuilder.AlterColumn<long>(
                name: "actor_employee_id",
                table: "catalogue_audit_event",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
