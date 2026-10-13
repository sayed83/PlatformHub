using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

public partial class AddAccountLoginLockout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedLoginAttempts",
            schema: "dbo",
            table: "AppUsers",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockoutEndUtc",
            schema: "dbo",
            table: "AppUsers",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FailedLoginAttempts", schema: "dbo", table: "AppUsers");
        migrationBuilder.DropColumn(name: "LockoutEndUtc", schema: "dbo", table: "AppUsers");
    }
}
