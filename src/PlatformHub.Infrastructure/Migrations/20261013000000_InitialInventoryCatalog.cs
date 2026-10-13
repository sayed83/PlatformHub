using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20261013000000_InitialInventoryCatalog")]
public sealed class InitialInventoryCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Products",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                BranchId = table.Column<long>(type: "bigint", nullable: true),
                ProjectId = table.Column<long>(type: "bigint", nullable: true),
                Sku = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Products", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId_BranchId_ProjectId_Sku",
            schema: "dbo",
            table: "Products",
            columns: new[] { "TenantId", "BranchId", "ProjectId", "Sku" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Products_TenantId_BranchId_ProjectId_IsActive",
            schema: "dbo",
            table: "Products",
            columns: new[] { "TenantId", "BranchId", "ProjectId", "IsActive" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Products", schema: "dbo");
    }
}
