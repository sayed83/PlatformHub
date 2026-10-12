using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261012190000_MakeProjectTenantOptional")]
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

        migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE dbo.usp_Platform_GetProjects
    @TenantId BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.Id, p.TenantId, t.Name AS TenantName, p.Code, p.Name, p.BaseUrl, p.IsActive
    FROM dbo.Projects AS p
    LEFT JOIN dbo.Tenants AS t ON t.Id = p.TenantId
    WHERE @TenantId IS NULL OR p.TenantId = @TenantId
    ORDER BY CASE WHEN p.TenantId IS NULL THEN 1 ELSE 0 END, t.Name, p.Name;
END;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Platform_GetProjects;");
        migrationBuilder.DropIndex(name: "IX_Projects_Code", schema: "dbo", table: "Projects");
        migrationBuilder.DropIndex(name: "IX_Projects_TenantId_Code", schema: "dbo", table: "Projects");
        migrationBuilder.AlterColumn<long>(name: "TenantId", schema: "dbo", table: "Projects",
            type: "bigint", nullable: false, oldClrType: typeof(long), oldType: "bigint", oldNullable: true);
        migrationBuilder.CreateIndex(name: "IX_Projects_TenantId_Code", schema: "dbo", table: "Projects",
            columns: new[] { "TenantId", "Code" }, unique: true);
    }
}
