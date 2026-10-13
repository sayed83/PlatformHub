using Microsoft.EntityFrameworkCore;
using PlatformHub.Domain.Entities;

namespace PlatformHub.Infrastructure.Persistence;

/// <summary>
/// Business-data context kept separate from platform identity/administration tables.
/// Uses the same SQL Server connection but an independent migrations history table.
/// </summary>
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<InventoryItem> Products => Set<InventoryItem>();
    public DbSet<AccountHead> AccHeads => Set<AccountHead>();
    public DbSet<SystemAccHeadSetting> SystemAccHeadSettings => Set<SystemAccHeadSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<InventoryItem>(e =>
        {
            e.ToTable("Products", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            e.Property(x => x.CreatedUtc).HasColumnType("datetime2");
            e.Property(x => x.UpdatedUtc).HasColumnType("datetime2");
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.ProjectId, x.Sku }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.ProjectId, x.IsActive });
        });

        modelBuilder.Entity<AccountHead>(e =>
        {
            e.ToTable("AccHeads", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.AccHeadName).HasMaxLength(100).IsRequired();
            e.Property(x => x.AccHeadCode).HasMaxLength(20);
            e.Property(x => x.AccType).HasMaxLength(50).IsRequired();
            e.Property(x => x.AccCategory).HasMaxLength(50).IsRequired();
            e.Property(x => x.TransFrom).HasMaxLength(100);
            e.Property(x => x.DefaultTaxRate).HasPrecision(5, 2);
            e.Property(x => x.PaymentType).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.NatureOfGroup).HasMaxLength(20);
            e.Property(x => x.AccountPurpose).HasMaxLength(100);
            e.Property(x => x.CashFlowType).HasMaxLength(20);
            e.Property(x => x.TaxCategory).HasMaxLength(50);
            e.Property(x => x.BudgetAmount).HasPrecision(18, 2);
            e.Property(x => x.CreatedUtc).HasColumnType("datetime2");
            e.Property(x => x.UpdatedUtc).HasColumnType("datetime2");
            e.HasOne(x => x.ParentAccHead)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentAccHeadId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.ProjectId, x.AccHeadCode }).IsUnique()
                .HasFilter("[AccHeadCode] IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.ProjectId, x.IsActive });
        });

        modelBuilder.Entity<SystemAccHeadSetting>(e =>
        {
            e.ToTable("SystemAccHeadSettings", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.SystemName).HasMaxLength(50).IsRequired();
            e.Property(x => x.CreateDate).HasColumnType("datetime2");
            e.HasOne(x => x.AccHead).WithMany().HasForeignKey(x => x.AccHeadId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.SystemName }).IsUnique();
            e.HasIndex(x => x.AccHeadId);
        });
    }
}
