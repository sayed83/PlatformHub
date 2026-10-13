using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261013170000_AddRefreshSessions")]
public sealed class AddRefreshSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RefreshSessions",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<long>(type: "bigint", nullable: false),
                TokenHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RevokedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReplacedByTokenHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_RefreshSessions_AppUsers_UserId",
                    column: x => x.UserId,
                    principalSchema: "dbo",
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_TokenHash",
            schema: "dbo",
            table: "RefreshSessions",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_UserId_RevokedUtc_ExpiresUtc",
            schema: "dbo",
            table: "RefreshSessions",
            columns: new[] { "UserId", "RevokedUtc", "ExpiresUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RefreshSessions", schema: "dbo");
    }
}
