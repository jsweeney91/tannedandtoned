using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using tanningTribe.Models;

namespace tanningTribe.Data;

public class StoreDbContext(DbContextOptions<StoreDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ReferralCode> ReferralCodes => Set<ReferralCode>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasIndex(p => p.Slug).IsUnique();
        modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.DiscountAmount).HasPrecision(18, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<ReferralCode>().HasIndex(r => r.Code).IsUnique();
        modelBuilder.Entity<ReferralCode>().HasIndex(r => r.OwnerOrderId).IsUnique();
        modelBuilder.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();
    }
}
