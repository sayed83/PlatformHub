using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

public partial class AddTenantOwnershipToProjects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "TenantId",
            table: "Projects",
            type: "bigint",
            nullable: true);

        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.Projects)
    THROW 51000, 'Project tenant migration stopped: assign each existing project to a tenant before applying this migration.', 1;
");

        migrationBuilder.AlterColumn<long>(
            name: "TenantId",
            table: "Projects",
            type: "bigint",
            nullable: false,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.DropIndex(
            name: "IX_Projects_Code",
            table: "Projects");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_TenantId_Code",
            table: "Projects",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Projects_Tenants_TenantId",
            table: "Projects",
            column: "TenantId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Projects_Tenants_TenantId",
            table: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_Projects_TenantId_Code",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Projects");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_Code",
            table: "Projects",
            column: "Code",
            unique: true);
    }
}
