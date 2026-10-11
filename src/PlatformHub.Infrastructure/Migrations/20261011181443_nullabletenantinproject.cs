using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class nullabletenantinproject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "ModuleId",
                schema: "dbo",
                table: "MenuItems",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "ModuleId",
                schema: "dbo",
                table: "MenuItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
