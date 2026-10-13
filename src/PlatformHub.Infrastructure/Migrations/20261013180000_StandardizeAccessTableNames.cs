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

        migrationBuilder.Sql("""
            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.PK_RoleWiseMenuAccess',
                N'PK_RoleWiseMenuAccesses', N'OBJECT';
            EXEC sp_rename N'dbo.UserWiseMenuAccesses.PK_UserWiseMenuAccess',
                N'PK_UserWiseMenuAccesses', N'OBJECT';

            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.FK_RoleWiseMenuAccess_MenuItems_MenuItemId',
                N'FK_RoleWiseMenuAccesses_MenuItems_MenuItemId', N'OBJECT';
            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.FK_RoleWiseMenuAccess_Projects_ProjectId',
                N'FK_RoleWiseMenuAccesses_Projects_ProjectId', N'OBJECT';
            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.FK_RoleWiseMenuAccess_Roles_RoleId',
                N'FK_RoleWiseMenuAccesses_Roles_RoleId', N'OBJECT';

            EXEC sp_rename N'dbo.UserWiseMenuAccesses.FK_UserWiseMenuAccess_AppUsers_UserId',
                N'FK_UserWiseMenuAccesses_AppUsers_UserId', N'OBJECT';
            EXEC sp_rename N'dbo.UserWiseMenuAccesses.FK_UserWiseMenuAccess_MenuItems_MenuItemId',
                N'FK_UserWiseMenuAccesses_MenuItems_MenuItemId', N'OBJECT';
            EXEC sp_rename N'dbo.UserWiseMenuAccesses.FK_UserWiseMenuAccess_Projects_ProjectId',
                N'FK_UserWiseMenuAccesses_Projects_ProjectId', N'OBJECT';
            """);

        migrationBuilder.RenameIndex(
            name: "IX_RoleWiseMenuAccess_MenuItemId",
            schema: "dbo",
            table: "RoleWiseMenuAccesses",
            newName: "IX_RoleWiseMenuAccesses_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_RoleWiseMenuAccess_ProjectId_RoleId_MenuItemId",
            schema: "dbo",
            table: "RoleWiseMenuAccesses",
            newName: "IX_RoleWiseMenuAccesses_ProjectId_RoleId_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_RoleWiseMenuAccess_RoleId",
            schema: "dbo",
            table: "RoleWiseMenuAccesses",
            newName: "IX_RoleWiseMenuAccesses_RoleId");

        migrationBuilder.RenameIndex(
            name: "IX_UserWiseMenuAccess_MenuItemId",
            schema: "dbo",
            table: "UserWiseMenuAccesses",
            newName: "IX_UserWiseMenuAccesses_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_UserWiseMenuAccess_ProjectId_UserId_MenuItemId",
            schema: "dbo",
            table: "UserWiseMenuAccesses",
            newName: "IX_UserWiseMenuAccesses_ProjectId_UserId_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_UserWiseMenuAccess_UserId",
            schema: "dbo",
            table: "UserWiseMenuAccesses",
            newName: "IX_UserWiseMenuAccesses_UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameIndex(
            name: "IX_RoleWiseMenuAccesses_MenuItemId",
            schema: "dbo",
            table: "RoleWiseMenuAccesses",
            newName: "IX_RoleWiseMenuAccess_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_RoleWiseMenuAccesses_ProjectId_RoleId_MenuItemId",
            schema: "dbo",
            table: "RoleWiseMenuAccesses",
            newName: "IX_RoleWiseMenuAccess_ProjectId_RoleId_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_RoleWiseMenuAccesses_RoleId",
            schema: "dbo",
            table: "RoleWiseMenuAccesses",
            newName: "IX_RoleWiseMenuAccess_RoleId");

        migrationBuilder.RenameIndex(
            name: "IX_UserWiseMenuAccesses_MenuItemId",
            schema: "dbo",
            table: "UserWiseMenuAccesses",
            newName: "IX_UserWiseMenuAccess_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_UserWiseMenuAccesses_ProjectId_UserId_MenuItemId",
            schema: "dbo",
            table: "UserWiseMenuAccesses",
            newName: "IX_UserWiseMenuAccess_ProjectId_UserId_MenuItemId");
        migrationBuilder.RenameIndex(
            name: "IX_UserWiseMenuAccesses_UserId",
            schema: "dbo",
            table: "UserWiseMenuAccesses",
            newName: "IX_UserWiseMenuAccess_UserId");

        migrationBuilder.Sql("""
            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.PK_RoleWiseMenuAccesses',
                N'PK_RoleWiseMenuAccess', N'OBJECT';
            EXEC sp_rename N'dbo.UserWiseMenuAccesses.PK_UserWiseMenuAccesses',
                N'PK_UserWiseMenuAccess', N'OBJECT';

            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.FK_RoleWiseMenuAccesses_MenuItems_MenuItemId',
                N'FK_RoleWiseMenuAccess_MenuItems_MenuItemId', N'OBJECT';
            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.FK_RoleWiseMenuAccesses_Projects_ProjectId',
                N'FK_RoleWiseMenuAccess_Projects_ProjectId', N'OBJECT';
            EXEC sp_rename N'dbo.RoleWiseMenuAccesses.FK_RoleWiseMenuAccesses_Roles_RoleId',
                N'FK_RoleWiseMenuAccess_Roles_RoleId', N'OBJECT';

            EXEC sp_rename N'dbo.UserWiseMenuAccesses.FK_UserWiseMenuAccesses_AppUsers_UserId',
                N'FK_UserWiseMenuAccess_AppUsers_UserId', N'OBJECT';
            EXEC sp_rename N'dbo.UserWiseMenuAccesses.FK_UserWiseMenuAccesses_MenuItems_MenuItemId',
                N'FK_UserWiseMenuAccess_MenuItems_MenuItemId', N'OBJECT';
            EXEC sp_rename N'dbo.UserWiseMenuAccesses.FK_UserWiseMenuAccesses_Projects_ProjectId',
                N'FK_UserWiseMenuAccess_Projects_ProjectId', N'OBJECT';
            """);

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
