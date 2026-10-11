using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class modification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            //migrationBuilder.RenameTable(
            //    name: "Tenants",
            //    newName: "Tenants",
            //    newSchema: "dbo");

            //migrationBuilder.RenameTable(
            //    name: "Projects",
            //    newName: "Projects",
            //    newSchema: "dbo");

            //migrationBuilder.RenameTable(
            //    name: "AppUsers",
            //    newName: "AppUsers",
            //    newSchema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Tenants",
                schema: "dbo",
                newName: "Tenants");

            migrationBuilder.RenameTable(
                name: "Projects",
                schema: "dbo",
                newName: "Projects");

            migrationBuilder.RenameTable(
                name: "AppUsers",
                schema: "dbo",
                newName: "AppUsers");
        }
    }
}
