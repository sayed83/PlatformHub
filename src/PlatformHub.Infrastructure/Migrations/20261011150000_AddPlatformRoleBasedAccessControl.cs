using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

[DbContext(typeof(PlatformDbContext))]
[Migration("20261011150000_AddPlatformRoleBasedAccessControl")]
public partial class AddPlatformRoleBasedAccessControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Permissions", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsSystemPermission = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Permissions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Roles", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                BaseRedirectUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Roles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "RolePermissions", schema: "dbo",
            columns: table => new
            {
                RoleId = table.Column<long>(type: "bigint", nullable: false),
                PermissionId = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                table.ForeignKey(name: "FK_RolePermissions_Roles_RoleId", column: x => x.RoleId, principalTable: "Roles", principalColumn: "Id", principalSchema: "dbo", onDelete: ReferentialAction.Cascade);
                table.ForeignKey(name: "FK_RolePermissions_Permissions_PermissionId", column: x => x.PermissionId, principalTable: "Permissions", principalColumn: "Id", principalSchema: "dbo", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserRoleAssignments", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<long>(type: "bigint", nullable: false),
                RoleId = table.Column<long>(type: "bigint", nullable: false),
                TenantId = table.Column<long>(type: "bigint", nullable: true),
                BranchId = table.Column<long>(type: "bigint", nullable: true),
                ProjectId = table.Column<long>(type: "bigint", nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                table.ForeignKey(name: "FK_UserRoleAssignments_AppUsers_UserId", column: x => x.UserId, principalTable: "AppUsers", principalColumn: "Id", principalSchema: "dbo", onDelete: ReferentialAction.Cascade);
                table.ForeignKey(name: "FK_UserRoleAssignments_Roles_RoleId", column: x => x.RoleId, principalTable: "Roles", principalColumn: "Id", principalSchema: "dbo", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_UserRoleAssignments_Tenants_TenantId", column: x => x.TenantId, principalTable: "Tenants", principalColumn: "Id", principalSchema: "dbo", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_UserRoleAssignments_Branches_BranchId", column: x => x.BranchId, principalTable: "Branches", principalColumn: "Id", principalSchema: "dbo", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_UserRoleAssignments_Projects_ProjectId", column: x => x.ProjectId, principalTable: "Projects", principalColumn: "Id", principalSchema: "dbo", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_Permissions_Code", schema: "dbo", table: "Permissions", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Roles_Code", schema: "dbo", table: "Roles", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_RolePermissions_PermissionId", schema: "dbo", table: "RolePermissions", column: "PermissionId");
        migrationBuilder.CreateIndex(name: "IX_UserRoleAssignments_UserId_RoleId_TenantId_BranchId_ProjectId", schema: "dbo", table: "UserRoleAssignments", columns: new[] { "UserId", "RoleId", "TenantId", "BranchId", "ProjectId" });
        migrationBuilder.CreateIndex(name: "IX_UserRoleAssignments_RoleId", schema: "dbo", table: "UserRoleAssignments", column: "RoleId");
        migrationBuilder.CreateIndex(name: "IX_UserRoleAssignments_TenantId", schema: "dbo", table: "UserRoleAssignments", column: "TenantId");
        migrationBuilder.CreateIndex(name: "IX_UserRoleAssignments_BranchId", schema: "dbo", table: "UserRoleAssignments", column: "BranchId");
        migrationBuilder.CreateIndex(name: "IX_UserRoleAssignments_ProjectId", schema: "dbo", table: "UserRoleAssignments", column: "ProjectId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RolePermissions", schema: "dbo");
        migrationBuilder.DropTable(name: "UserRoleAssignments", schema: "dbo");
        migrationBuilder.DropTable(name: "Permissions", schema: "dbo");
        migrationBuilder.DropTable(name: "Roles", schema: "dbo");
    }
}
