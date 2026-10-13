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
    public DbSet<AccountHead> AccountHeads => Set<AccountHead>();

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
            e.ToTable("AccountHeads", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.AccountType).HasMaxLength(50).IsRequired();
            e.Property(x => x.CreatedUtc).HasColumnType("datetime2");
            e.Property(x => x.UpdatedUtc).HasColumnType("datetime2");
            e.HasOne<AccountHead>()
                .WithMany()
                .HasForeignKey(x => x.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.ProjectId, x.Code }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.BranchId, x.ProjectId, x.IsActive });
        });
    }
}
