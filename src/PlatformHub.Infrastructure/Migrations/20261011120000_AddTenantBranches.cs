using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261011120000_AddTenantBranches")]
public partial class AddTenantBranches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Branches",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Branches", x => x.Id);
                table.ForeignKey(
                    name: "FK_Branches_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalSchema: "dbo",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Branches_TenantId_Code",
            schema: "dbo",
            table: "Branches",
            columns: new[] { "TenantId", "Code" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Branches", schema: "dbo");
    }
}
