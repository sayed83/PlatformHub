using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using PlatformHub.Infrastructure.Persistence;

#nullable disable

namespace PlatformHub.Infrastructure.Migrations
{
    [DbContext(typeof(PlatformDbContext))]
    partial class PlatformDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618

            modelBuilder
                .HasDefaultSchema("dbo")
                .HasAnnotation("ProductVersion", "8.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("PlatformHub.Domain.Entities.AppUser", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("bigint");

                SqlServerPropertyBuilderExtensions.UseIdentityColumn(
                    b.Property<long>("Id"));

                b.Property<string>("BaseRedirectUrl")
                    .HasMaxLength(500)
                    .HasColumnType("nvarchar(500)");

                b.Property<DateTime>("CreatedUtc")
                    .HasColumnType("datetime2");

                b.Property<string>("DisplayName")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<bool>("IsActive").HasColumnType("bit");
                b.Property<bool>("IsPlatformAdmin").HasColumnType("bit");

                b.Property<string>("PasswordHash")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("nvarchar(500)");

                b.Property<string>("UserName")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("nvarchar(100)");

                b.HasKey("Id");
                b.HasIndex("UserName").IsUnique();
                b.ToTable("AppUsers", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.Tenant", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("bigint");

                SqlServerPropertyBuilderExtensions.UseIdentityColumn(
                    b.Property<long>("Id"));

                b.Property<string>("Code")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<bool>("IsActive").HasColumnType("bit");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.HasKey("Id");
                b.HasIndex("Code").IsUnique();
                b.ToTable("Tenants", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.Project", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("bigint");

                SqlServerPropertyBuilderExtensions.UseIdentityColumn(
                    b.Property<long>("Id"));

                b.Property<string>("BaseUrl")
                    .HasColumnType("nvarchar(max)");

                b.Property<string>("Code")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<bool>("IsActive").HasColumnType("bit");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<long>("TenantId").HasColumnType("bigint");

                b.HasKey("Id");
                b.HasIndex("TenantId", "Code").IsUnique();
                b.ToTable("Projects", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.Branch", b =>
            {
                b.Property<long>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("bigint");

                SqlServerPropertyBuilderExtensions.UseIdentityColumn(
                    b.Property<long>("Id"));

                b.Property<string>("Address")
                    .HasMaxLength(500)
                    .HasColumnType("nvarchar(500)");

                b.Property<string>("Code")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("nvarchar(50)");

                b.Property<bool>("IsActive").HasColumnType("bit");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("nvarchar(200)");

                b.Property<long>("TenantId").HasColumnType("bigint");

                b.HasKey("Id");
                b.HasIndex("TenantId", "Code").IsUnique();
                b.ToTable("Branches", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.PlatformRole", b =>
            {
                b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
                SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));
                b.Property<string>("BaseRedirectUrl").HasMaxLength(500).HasColumnType("nvarchar(500)");
                b.Property<string>("Code").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
                b.Property<DateTime>("CreatedUtc").HasColumnType("datetime2");
                b.Property<string>("Description").HasMaxLength(500).HasColumnType("nvarchar(500)");
                b.Property<bool>("IsActive").HasColumnType("bit");
                b.Property<bool>("IsSystemRole").HasColumnType("bit");
                b.Property<string>("Name").IsRequired().HasMaxLength(150).HasColumnType("nvarchar(150)");
                b.HasKey("Id");
                b.HasIndex("Code").IsUnique();
                b.ToTable("Roles", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.PlatformPermission", b =>
            {
                b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
                SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));
                b.Property<string>("Code").IsRequired().HasMaxLength(150).HasColumnType("nvarchar(150)");
                b.Property<string>("Description").HasMaxLength(500).HasColumnType("nvarchar(500)");
                b.Property<bool>("IsSystemPermission").HasColumnType("bit");
                b.Property<string>("Module").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
                b.Property<string>("Name").IsRequired().HasMaxLength(150).HasColumnType("nvarchar(150)");
                b.HasKey("Id");
                b.HasIndex("Code").IsUnique();
                b.ToTable("Permissions", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.PlatformRolePermission", b =>
            {
                b.Property<long>("RoleId").HasColumnType("bigint");
                b.Property<long>("PermissionId").HasColumnType("bigint");
                b.HasKey("RoleId", "PermissionId");
                b.HasIndex("PermissionId");
                b.ToTable("RolePermissions", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.UserRoleAssignment", b =>
            {
                b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint");
                SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));
                b.Property<long?>("BranchId").HasColumnType("bigint");
                b.Property<DateTime>("CreatedUtc").HasColumnType("datetime2");
                b.Property<long?>("ProjectId").HasColumnType("bigint");
                b.Property<long>("RoleId").HasColumnType("bigint");
                b.Property<long?>("TenantId").HasColumnType("bigint");
                b.Property<long>("UserId").HasColumnType("bigint");
                b.HasKey("Id");
                b.HasIndex("BranchId");
                b.HasIndex("ProjectId");
                b.HasIndex("RoleId");
                b.HasIndex("TenantId");
                b.HasIndex("UserId", "RoleId", "TenantId", "BranchId", "ProjectId");
                b.ToTable("UserRoleAssignments", "dbo");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.PlatformRolePermission", b =>
            {
                b.HasOne("PlatformHub.Domain.Entities.PlatformPermission", "Permission")
                    .WithMany().HasForeignKey("PermissionId").OnDelete(DeleteBehavior.Cascade).IsRequired();
                b.HasOne("PlatformHub.Domain.Entities.PlatformRole", "Role")
                    .WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade).IsRequired();
                b.Navigation("Permission");
                b.Navigation("Role");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.UserRoleAssignment", b =>
            {
                b.HasOne("PlatformHub.Domain.Entities.Branch", "Branch")
                    .WithMany().HasForeignKey("BranchId").OnDelete(DeleteBehavior.Restrict);
                b.HasOne("PlatformHub.Domain.Entities.Project", "Project")
                    .WithMany().HasForeignKey("ProjectId").OnDelete(DeleteBehavior.Restrict);
                b.HasOne("PlatformHub.Domain.Entities.PlatformRole", "Role")
                    .WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Restrict).IsRequired();
                b.HasOne("PlatformHub.Domain.Entities.Tenant", "Tenant")
                    .WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
                b.HasOne("PlatformHub.Domain.Entities.AppUser", "User")
                    .WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade).IsRequired();
                b.Navigation("Branch");
                b.Navigation("Project");
                b.Navigation("Role");
                b.Navigation("Tenant");
                b.Navigation("User");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.Project", b =>
            {
                b.HasOne("PlatformHub.Domain.Entities.Tenant", "Tenant")
                    .WithMany()
                    .HasForeignKey("TenantId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.Navigation("Tenant");
            });

            modelBuilder.Entity("PlatformHub.Domain.Entities.Branch", b =>
            {
                b.HasOne("PlatformHub.Domain.Entities.Tenant", "Tenant")
                    .WithMany()
                    .HasForeignKey("TenantId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();

                b.Navigation("Tenant");
            });

#pragma warning restore 612, 618
        }
    }
}
