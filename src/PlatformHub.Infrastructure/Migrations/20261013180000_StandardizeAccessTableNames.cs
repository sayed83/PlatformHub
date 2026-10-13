using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261013180000_StandardizeAccessTableNames")]
public sealed class StandardizeAccessTableNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "RoleWiseMenuAccess",
            schema: "dbo",
            newName: "RoleWiseMenuAccesses",
            newSchema: "dbo");

        migrationBuilder.RenameTable(
            name: "UserWiseMenuAccess",
            schema: "dbo",
            newName: "UserWiseMenuAccesses",
            newSchema: "dbo");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "RoleWiseMenuAccesses",
            schema: "dbo",
            newName: "RoleWiseMenuAccess",
            newSchema: "dbo");

        migrationBuilder.RenameTable(
            name: "UserWiseMenuAccesses",
            schema: "dbo",
            newName: "UserWiseMenuAccess",
            newSchema: "dbo");
    }
}
