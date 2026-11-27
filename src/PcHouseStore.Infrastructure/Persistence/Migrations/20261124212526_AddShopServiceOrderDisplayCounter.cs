using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopServiceOrderDisplayCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "next_service_order_display_number",
                table: "shop",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_next_service_order_display_number",
                table: "shop",
                sql: "next_service_order_display_number > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_next_service_order_display_number",
                table: "shop");

            migrationBuilder.DropColumn(
                name: "next_service_order_display_number",
                table: "shop");
        }
    }
}
