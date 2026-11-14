using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAndCatalogueFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalogue_audit_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    actor_employee_id = table.Column<long>(type: "bigint", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    resource_id = table.Column<long>(type: "bigint", nullable: false),
                    action_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_audit_event", x => x.id);
                    table.ForeignKey(
                        name: "FK_catalogue_audit_event_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalogue_audit_event_employee_actor_employee_id_company_id",
                        columns: x => new { x.actor_employee_id, x.company_id },
                        principalTable: "employee",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalogue_audit_event_shop_shop_id_company_id",
                        columns: x => new { x.shop_id, x.company_id },
                        principalTable: "shop",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    person_id = table.Column<long>(type: "bigint", nullable: false),
                    phone_e164 = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.id);
                    table.UniqueConstraint("ak_customer_id_company", x => new { x.id, x.company_id });
                    table.CheckConstraint("ck_customer_phone_e164", "phone_e164 ~ '^\\+[1-9][0-9]{7,14}$'");
                    table.ForeignKey(
                        name: "FK_customer_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_person_person_id_company_id",
                        columns: x => new { x.person_id, x.company_id },
                        principalTable: "person",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fault",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fault", x => x.id);
                    table.UniqueConstraint("ak_fault_id_company", x => new { x.id, x.company_id });
                    table.CheckConstraint("ck_fault_sequence_number", "sequence_number > 0");
                    table.ForeignKey(
                        name: "FK_fault_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_category",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_category_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_category", x => x.id);
                    table.UniqueConstraint("ak_product_category_id_company", x => new { x.id, x.company_id });
                    table.ForeignKey(
                        name: "FK_product_category_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_category_product_category_parent_category_id_compan~",
                        columns: x => new { x.parent_category_id, x.company_id },
                        principalTable: "product_category",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    list_price = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    product_category_id = table.Column<long>(type: "bigint", nullable: true),
                    inventory_tracking_mode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    service_description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_item", x => x.id);
                    table.UniqueConstraint("ak_catalog_item_id_company", x => new { x.id, x.company_id });
                    table.CheckConstraint("ck_catalog_item_kind", "kind IN ('PRODUCT', 'SERVICE')");
                    table.CheckConstraint("ck_catalog_item_price", "list_price >= 0");
                    table.CheckConstraint("ck_catalog_item_sku_format", "sku IS NULL OR (sku = upper(sku) AND sku ~ '^SKU-[A-Z0-9]{8}$')");
                    table.CheckConstraint("ck_catalog_item_subtype", "(kind = 'PRODUCT' AND product_category_id IS NOT NULL AND inventory_tracking_mode IN ('QUANTITY', 'SERIALIZED_UNIT') AND service_description IS NULL) OR (kind = 'SERVICE' AND product_category_id IS NULL AND inventory_tracking_mode IS NULL AND service_description IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_catalog_item_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_item_product_category_product_category_id_company_id",
                        columns: x => new { x.product_category_id, x.company_id },
                        principalTable: "product_category",
                        principalColumns: new[] { "id", "company_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_item_company_kind_active_name",
                table: "catalog_item",
                columns: new[] { "company_id", "kind", "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_item_product_category_id_company_id",
                table: "catalog_item",
                columns: new[] { "product_category_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_catalog_item_company_sku",
                table: "catalog_item",
                columns: new[] { "company_id", "sku" },
                unique: true,
                filter: "sku IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_audit_event_actor_employee_id_company_id",
                table: "catalogue_audit_event",
                columns: new[] { "actor_employee_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_audit_event_company_time",
                table: "catalogue_audit_event",
                columns: new[] { "company_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_catalogue_audit_event_resource_time",
                table: "catalogue_audit_event",
                columns: new[] { "resource_type", "resource_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_audit_event_shop_id_company_id",
                table: "catalogue_audit_event",
                columns: new[] { "shop_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_company_active",
                table: "customer",
                columns: new[] { "company_id", "is_active", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_person_id_company_id",
                table: "customer",
                columns: new[] { "person_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_customer_company_phone_e164",
                table: "customer",
                columns: new[] { "company_id", "phone_e164" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fault_company_active_description",
                table: "fault",
                columns: new[] { "company_id", "is_active", "description" });

            migrationBuilder.CreateIndex(
                name: "uq_fault_company_code",
                table: "fault",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_fault_company_sequence",
                table: "fault",
                columns: new[] { "company_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_category_parent_category_id_company_id",
                table: "product_category",
                columns: new[] { "parent_category_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_product_category_company_code",
                table: "product_category",
                columns: new[] { "company_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_item");

            migrationBuilder.DropTable(
                name: "catalogue_audit_event");

            migrationBuilder.DropTable(
                name: "customer");

            migrationBuilder.DropTable(
                name: "fault");

            migrationBuilder.DropTable(
                name: "product_category");
        }
    }
}
