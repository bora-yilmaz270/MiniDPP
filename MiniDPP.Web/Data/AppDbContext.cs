using Microsoft.EntityFrameworkCore;
using MiniDPP.Web.Models;

namespace MiniDPP.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProductModel> ProductModels => Set<ProductModel>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<ProductVersion> ProductVersions => Set<ProductVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductModel>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Gtin).IsUnique();
            entity.Property(x => x.Gtin).HasMaxLength(14).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasMany(x => x.Batches).WithOne(x => x.ProductModel)
                .HasForeignKey(x => x.ProductModelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Versions).WithOne(x => x.ProductModel)
                .HasForeignKey(x => x.ProductModelId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductBatch>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProductModelId, x.BatchNo }).IsUnique();
            entity.Property(x => x.BatchNo).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProductionPlace).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<ProductVersion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProductModelId, x.VersionNumber }).IsUnique();
            entity.Property(x => x.SnapshotJson).IsRequired();
        });
    }
}
