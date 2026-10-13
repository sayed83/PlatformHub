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
            name: "AccHeads",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                BranchId = table.Column<long>(type: "bigint", nullable: true),
                ProjectId = table.Column<long>(type: "bigint", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                AccHeadName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                AccHeadCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                ParentAccHeadId = table.Column<long>(type: "bigint", nullable: true),
                AccType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                AccCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Level = table.Column<int>(type: "int", nullable: false),
                TransId = table.Column<long>(type: "bigint", nullable: true),
                TransFrom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                IsTaxable = table.Column<bool>(type: "bit", nullable: false),
                DefaultTaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                IsSystemAccount = table.Column<bool>(type: "bit", nullable: false),
                IsTaxInclusive = table.Column<bool>(type: "bit", nullable: false),
                IsGroup = table.Column<bool>(type: "bit", nullable: false),
                IsEditable = table.Column<bool>(type: "bit", nullable: false),
                IsDeletable = table.Column<bool>(type: "bit", nullable: false),
                PaymentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                NatureOfGroup = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                AccountPurpose = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CashFlowType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                ReportSequence = table.Column<int>(type: "int", nullable: true),
                TaxCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                BudgetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccHeads", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccHeads_AccHeads_ParentAccHeadId",
                    column: x => x.ParentAccHeadId,
                    principalSchema: "dbo",
                    principalTable: "AccHeads",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccHeads_TenantId_BranchId_ProjectId_AccHeadCode",
            schema: "dbo",
            table: "AccHeads",
            columns: new[] { "TenantId", "BranchId", "ProjectId", "AccHeadCode" },
            unique: true,
            filter: "[AccHeadCode] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AccHeads_TenantId_BranchId_ProjectId_IsActive",
            schema: "dbo",
            table: "AccHeads",
            columns: new[] { "TenantId", "BranchId", "ProjectId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_AccHeads_ParentAccHeadId",
            schema: "dbo",
            table: "AccHeads",
            column: "ParentAccHeadId");

        migrationBuilder.CreateTable(
            name: "SystemAccHeadSettings",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AccHeadId = table.Column<long>(type: "bigint", nullable: false),
                SystemName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                BranchId = table.Column<long>(type: "bigint", nullable: true),
                TenantId = table.Column<long>(type: "bigint", nullable: false),
                CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SystemAccHeadSettings", x => x.Id);
                table.ForeignKey(
                    name: "FK_SystemAccHeadSettings_AccHeads_AccHeadId",
                    column: x => x.AccHeadId,
                    principalSchema: "dbo",
                    principalTable: "AccHeads",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SystemAccHeadSettings_AppUsers_CreatedBy",
                    column: x => x.CreatedBy,
                    principalSchema: "dbo",
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SystemAccHeadSettings_Branches_BranchId",
                    column: x => x.BranchId,
                    principalSchema: "dbo",
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SystemAccHeadSettings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalSchema: "dbo",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SystemAccHeadSettings_TenantId_BranchId_SystemName",
            schema: "dbo",
            table: "SystemAccHeadSettings",
            columns: new[] { "TenantId", "BranchId", "SystemName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SystemAccHeadSettings_AccHeadId",
            schema: "dbo",
            table: "SystemAccHeadSettings",
            column: "AccHeadId");

        migrationBuilder.CreateIndex(
            name: "IX_SystemAccHeadSettings_BranchId",
            schema: "dbo",
            table: "SystemAccHeadSettings",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_SystemAccHeadSettings_CreatedBy",
            schema: "dbo",
            table: "SystemAccHeadSettings",
            column: "CreatedBy");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SystemAccHeadSettings", schema: "dbo");
        migrationBuilder.DropTable(name: "AccHeads", schema: "dbo");
    }
}
