using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations;

//[DbContext(typeof(PlatformDbContext))]
//[Migration("20261012190000_MakeProjectTenantOptional")]
partial class MakeProjectTenantOptional
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // Migration discovery metadata is defined here; the current model snapshot
        // remains the authoritative generated target model for future migrations.
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11");
    }
}
