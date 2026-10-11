using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;

namespace PlatformHub.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<PlatformRole> Roles => Set<PlatformRole>();
    public DbSet<PlatformPermission> Permissions => Set<PlatformPermission>();
    public DbSet<PlatformRolePermission> RolePermissions => Set<PlatformRolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<ProjectModule> ProjectModules => Set<ProjectModule>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RoleWiseMenuAccess> RoleWiseMenuAccesses => Set<RoleWiseMenuAccess>();
    public DbSet<UserWiseMenuAccess> UserWiseMenuAccesses => Set<UserWiseMenuAccess>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("AppUsers", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.BaseRedirectUrl).HasMaxLength(500);
        });
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("Projects", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<Branch>(e =>
        {
            e.ToTable("Branches", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Address).HasMaxLength(500);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<PlatformRole>(e =>
        {
            e.ToTable("Roles", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.BaseRedirectUrl).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<PlatformPermission>(e =>
        {
            e.ToTable("Permissions", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(150).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Module).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<PlatformRolePermission>(e =>
        {
            e.ToTable("RolePermissions", "dbo");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserRoleAssignment>(e =>
        {
            e.ToTable("UserRoleAssignments", "dbo");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.RoleId, x.TenantId, x.BranchId, x.ProjectId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectModule>(e =>
        {
            e.ToTable("ProjectModules", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Icon).HasMaxLength(100);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.SortOrder });
        });
        modelBuilder.Entity<MenuItem>(e =>
        {
            e.ToTable("MenuItems", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(150).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Area).HasMaxLength(100);
            e.Property(x => x.Controller).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.RouteUrl).HasMaxLength(1000);
            e.Property(x => x.Icon).HasMaxLength(100);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(x => x.ParentMenu).WithMany().HasForeignKey(x => x.ParentMenuId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.ModuleId, x.ParentMenuId, x.SortOrder });
        });
        modelBuilder.Entity<RoleWiseMenuAccess>(e =>
        {
            e.ToTable("RoleWiseMenuAccess", "dbo");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ProjectId, x.RoleId, x.MenuItemId }).IsUnique();
        });
        modelBuilder.Entity<UserWiseMenuAccess>(e =>
        {
            e.ToTable("UserWiseMenuAccess", "dbo");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ProjectId, x.UserId, x.MenuItemId }).IsUnique();
        });
    }
}
