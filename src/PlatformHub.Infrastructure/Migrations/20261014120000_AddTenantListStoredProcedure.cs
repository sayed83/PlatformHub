using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261014120000_AddTenantListStoredProcedure")]
public sealed class AddTenantListStoredProcedure : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR ALTER PROCEDURE dbo.usp_Platform_GetTenants
            AS
            BEGIN
                SET NOCOUNT ON;

                SELECT Id, Code, Name, IsActive
                FROM dbo.Tenants
                ORDER BY Name, Id;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Platform_GetTenants;");
    }
}
