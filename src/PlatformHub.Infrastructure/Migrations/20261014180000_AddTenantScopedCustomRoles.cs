using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261014180000_AddTenantScopedCustomRoles")]
public partial class AddTenantScopedCustomRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "TenantId", schema: "dbo", table: "Roles", type: "bigint", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Roles_TenantId", schema: "dbo", table: "Roles", column: "TenantId");
        migrationBuilder.DropIndex(name: "IX_Roles_Code", schema: "dbo", table: "Roles");
        migrationBuilder.CreateIndex(name: "IX_Roles_TenantId_Code", schema: "dbo", table: "Roles", columns: new[] { "TenantId", "Code" }, unique: true, filter: "[TenantId] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_Roles_Code", schema: "dbo", table: "Roles", column: "Code", unique: true, filter: "[TenantId] IS NULL");
        migrationBuilder.AddForeignKey(name: "FK_Roles_Tenants_TenantId", schema: "dbo", table: "Roles", column: "TenantId", principalSchema: "dbo", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Roles_Tenants_TenantId", schema: "dbo", table: "Roles");
        migrationBuilder.DropIndex(name: "IX_Roles_TenantId_Code", schema: "dbo", table: "Roles");
        migrationBuilder.DropIndex(name: "IX_Roles_Code", schema: "dbo", table: "Roles");
        migrationBuilder.DropIndex(name: "IX_Roles_TenantId", schema: "dbo", table: "Roles");
        migrationBuilder.DropColumn(name: "TenantId", schema: "dbo", table: "Roles");
        migrationBuilder.CreateIndex(name: "IX_Roles_Code", schema: "dbo", table: "Roles", column: "Code", unique: true);
    }
}
