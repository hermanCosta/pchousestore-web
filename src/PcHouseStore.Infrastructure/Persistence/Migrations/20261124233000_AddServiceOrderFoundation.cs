using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations;

public partial class AddServiceOrderFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "service_order",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                company_id = table.Column<long>(type: "bigint", nullable: false),
                origin_shop_id = table.Column<long>(type: "bigint", nullable: false),
                shop_id = table.Column<long>(type: "bigint", nullable: false),
                display_number = table.Column<long>(type: "bigint", nullable: false),
                customer_id = table.Column<long>(type: "bigint", nullable: false),
                device_id = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                responsible_employee_id = table.Column<long>(type: "bigint", nullable: true),
                created_by_employee_id = table.Column<long>(type: "bigint", nullable: false),
                refurbished_unit_warranty_id = table.Column<long>(type: "bigint", nullable: true),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "CREATED"),
                technical_outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                currency_code = table.Column<string>(type: "character(3)", nullable: false, defaultValue: "EUR"),
                subtotal = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                discount = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                tax_total = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                total = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                balance_due = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                picked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                bad_sector = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_service_order", x => x.id);
                table.UniqueConstraint("ak_service_order_id_company", x => new { x.id, x.company_id });
                table.ForeignKey("FK_service_order_company_company_id", x => x.company_id, "company", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_service_order_shop_origin_shop_id_company_id", x => new { x.origin_shop_id, x.company_id }, "shop", new[] { "id", "company_id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_service_order_shop_shop_id_company_id", x => new { x.shop_id, x.company_id }, "shop", new[] { "id", "company_id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_service_order_customer_customer_id_company_id", x => new { x.customer_id, x.company_id }, "customer", new[] { "id", "company_id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_service_order_device_device_id_company_id", x => new { x.device_id, x.company_id }, "device", new[] { "id", "company_id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_service_order_employee_created_by_employee_id_company_id", x => new { x.created_by_employee_id, x.company_id }, "employee", new[] { "id", "company_id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_service_order_employee_responsible_employee_id_company_id", x => new { x.responsible_employee_id, x.company_id }, "employee", new[] { "id", "company_id" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_service_order_company_id", "service_order", "company_id");
        migrationBuilder.CreateIndex("IX_service_order_origin_shop_id_company_id", "service_order", new[] { "origin_shop_id", "company_id" });
        migrationBuilder.CreateIndex("IX_service_order_shop_id_company_id", "service_order", new[] { "shop_id", "company_id" });
        migrationBuilder.CreateIndex("IX_service_order_customer_id_company_id", "service_order", new[] { "customer_id", "company_id" });
        migrationBuilder.CreateIndex("IX_service_order_device_id_company_id", "service_order", new[] { "device_id", "company_id" });
        migrationBuilder.CreateIndex("IX_service_order_created_by_employee_id_company_id", "service_order", new[] { "created_by_employee_id", "company_id" });
        migrationBuilder.CreateIndex("IX_service_order_responsible_employee_id_company_id", "service_order", new[] { "responsible_employee_id", "company_id" });
        migrationBuilder.CreateIndex("uq_service_order_shop_display_number", "service_order", new[] { "shop_id", "display_number" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "service_order");
    }
}
