using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20261013010000_AddAccountHeads")]
public sealed class AddAccountHeads : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccountHeads",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                BranchId = table.Column<long>(type: "bigint", nullable: true),
                ProjectId = table.Column<long>(type: "bigint", nullable: true),
                Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AccountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ParentAccountId = table.Column<long>(type: "bigint", nullable: true),
                IsControlAccount = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountHeads", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccountHeads_AccountHeads_ParentAccountId",
                    column: x => x.ParentAccountId,
                    principalSchema: "dbo",
                    principalTable: "AccountHeads",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccountHeads_TenantId_BranchId_ProjectId_Code",
            schema: "dbo",
            table: "AccountHeads",
            columns: new[] { "TenantId", "BranchId", "ProjectId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AccountHeads_TenantId_BranchId_ProjectId_IsActive",
            schema: "dbo",
            table: "AccountHeads",
            columns: new[] { "TenantId", "BranchId", "ProjectId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_AccountHeads_ParentAccountId",
            schema: "dbo",
            table: "AccountHeads",
            column: "ParentAccountId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AccountHeads", schema: "dbo");
    }
}
