using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PcHouseStore.Infrastructure.Persistence;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PcHouseStoreDbContext))]
[Migration("20261124223000_AddDeviceFoundation")]
public partial class AddDeviceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "device",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                company_id = table.Column<long>(type: "bigint", nullable: false),
                brand = table.Column<string>(type: "text", nullable: false),
                model = table.Column<string>(type: "text", nullable: false),
                serial_number = table.Column<string>(type: "text", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_device", x => x.id);
                table.UniqueConstraint("ak_device_id_company", x => new { x.id, x.company_id });
                table.ForeignKey(
                    name: "FK_device_company_company_id",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "device");
    }
}
