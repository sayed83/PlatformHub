using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

public partial class MakeProjectTenantOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Projects_TenantId_Code", schema: "dbo", table: "Projects");
        migrationBuilder.AlterColumn<long>(name: "TenantId", schema: "dbo", table: "Projects",
            type: "bigint", nullable: true, oldClrType: typeof(long), oldType: "bigint");
        migrationBuilder.CreateIndex(name: "IX_Projects_TenantId_Code", schema: "dbo", table: "Projects",
            columns: new[] { "TenantId", "Code" }, unique: true, filter: "[TenantId] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_Projects_Code", schema: "dbo", table: "Projects",
            column: "Code", unique: true, filter: "[TenantId] IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Projects_Code", schema: "dbo", table: "Projects");
        migrationBuilder.DropIndex(name: "IX_Projects_TenantId_Code", schema: "dbo", table: "Projects");
        migrationBuilder.AlterColumn<long>(name: "TenantId", schema: "dbo", table: "Projects",
            type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_Projects_TenantId_Code", schema: "dbo", table: "Projects",
            columns: new[] { "TenantId", "Code" }, unique: true);
    }
}
