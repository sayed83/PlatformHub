using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261013100000_AddTenantProjectAssignments")]
public partial class AddTenantProjectAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TenantProjects",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                ProjectId = table.Column<long>(type: "bigint", nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                AssignedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantProjects", x => x.Id);
                table.ForeignKey("FK_TenantProjects_Tenants_TenantId", x => x.TenantId,
                    principalSchema: "dbo", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_TenantProjects_Projects_ProjectId", x => x.ProjectId,
                    principalSchema: "dbo", principalTable: "Projects", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_TenantProjects_TenantId_ProjectId", schema: "dbo", table: "TenantProjects",
            columns: new[] { "TenantId", "ProjectId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_TenantProjects_TenantId_IsEnabled", schema: "dbo", table: "TenantProjects",
            columns: new[] { "TenantId", "IsEnabled" });
        migrationBuilder.CreateIndex(name: "IX_TenantProjects_ProjectId", schema: "dbo", table: "TenantProjects",
            column: "ProjectId");

        // Preserve existing tenant/project relationships during rollout.
        migrationBuilder.Sql("""
        INSERT INTO dbo.TenantProjects
            (TenantId, ProjectId, IsEnabled, AssignedUtc)
        SELECT
            p.TenantId, p.Id, 1, SYSUTCDATETIME()
        FROM dbo.Projects AS p
        INNER JOIN dbo.Tenants AS t ON t.Id = p.TenantId
        WHERE p.TenantId IS NOT NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.TenantProjects AS tp
              WHERE tp.TenantId = p.TenantId
                AND tp.ProjectId = p.Id
          );
        """);

        migrationBuilder.Sql("""
        CREATE OR ALTER PROCEDURE dbo.usp_Platform_GetProjects
            @TenantId BIGINT = NULL
        AS
        BEGIN
            SET NOCOUNT ON;

            SELECT
                p.Id,
                @TenantId AS TenantId,
                CASE
                    WHEN @TenantId IS NOT NULL
                        THEN selectedTenant.Name
                    ELSE N'Shared catalogue'
                END AS TenantName,
                p.Code,
                p.Name,
                p.BaseUrl,
                p.IsActive
            FROM dbo.Projects AS p
            LEFT JOIN dbo.Tenants AS selectedTenant
                ON selectedTenant.Id = @TenantId
            WHERE
                @TenantId IS NULL
                OR p.TenantId = @TenantId
                OR EXISTS
                (
                    SELECT 1
                    FROM dbo.TenantProjects AS tp
                    WHERE tp.TenantId = @TenantId
                      AND tp.ProjectId = p.Id
                      AND tp.IsEnabled = 1
                )
            ORDER BY p.Name;
        END;
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TenantProjects", schema: "dbo");
    }
}
