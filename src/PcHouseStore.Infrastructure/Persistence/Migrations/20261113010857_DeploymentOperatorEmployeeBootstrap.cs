using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcHouseStore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeploymentOperatorEmployeeBootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "actor_employee_id",
                table: "employee_administration_event",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "actor_identifier",
                table: "employee_administration_event",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_type",
                table: "employee_administration_event",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql("UPDATE employee_administration_event SET actor_type = 'EMPLOYEE' WHERE actor_type IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "actor_type",
                table: "employee_administration_event",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actor_identifier",
                table: "employee_administration_event");

            migrationBuilder.DropColumn(
                name: "actor_type",
                table: "employee_administration_event");

            migrationBuilder.AlterColumn<long>(
                name: "actor_employee_id",
                table: "employee_administration_event",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
