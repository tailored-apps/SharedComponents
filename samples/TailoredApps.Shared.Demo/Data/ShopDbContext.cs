using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Demo.Data;

public class Product : IModelBase<int>
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }
}

public enum OrderStatus
{
    New,
    AwaitingPayment,
    Paid,
    PaymentRejected,
}

public class Order : IModelBase<int>
{
    public int Id { get; set; }

    /// <summary>The public identifier of the order. It is also the reference sent to the payment gateway.</summary>
    public string Number { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "PLN";

    public OrderStatus Status { get; set; }

    public string? PaymentProvider { get; set; }

    public string? PaymentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
}

/// <summary>What the unit of work exposes to the application: the sets, not the whole DbContext.</summary>
public interface IShopDbContext
{
    DbSet<Product> Products { get; }

    DbSet<Order> Orders { get; }
}

public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options), IShopDbContext
{
    /// <summary>An amount with two decimal places, stored as a whole number of hundredths.</summary>
    private static readonly ValueConverter<decimal, long> MinorUnits = new(
        amount => (long)decimal.Round(amount * 100m, MidpointRounding.AwayFromZero),
        minorUnits => minorUnits / 100m);

    /// <summary>
    /// A point in time, stored as UTC ticks: exact, unlike DateTimeOffsetToBinaryConverter, which drops
    /// precision. The offset is not kept; the application records every time in UTC.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, long> UtcTicks = new(
        time => time.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite has no decimal or date-with-offset type and cannot sort or compare them as text. Amounts are
        // stored in minor units (grosze) - exact, unlike a floating-point column - and dates as numbers, so
        // that sorting by price works. A database with a decimal type needs none of this.
        modelBuilder.Entity<Product>().Property(p => p.Price).HasConversion(MinorUnits);
        modelBuilder.Entity<Order>().Property(o => o.Amount).HasConversion(MinorUnits);
        modelBuilder.Entity<Order>().Property(o => o.CreatedAt).HasConversion(UtcTicks);
        modelBuilder.Entity<Order>().Property(o => o.PaidAt).HasConversion(UtcTicks);
        modelBuilder.Entity<Order>().HasIndex(o => o.Number).IsUnique();
    }

    public static void Seed(ShopDbContext db)
    {
        db.Database.EnsureCreated();
        if (db.Products.Any())
            return;

        db.Products.AddRange(
            new Product { Name = "Green tea", Category = "tea", Price = 24.90m },
            new Product { Name = "Black tea", Category = "tea", Price = 19.50m },
            new Product { Name = "Oolong", Category = "tea", Price = 42.00m },
            new Product { Name = "Espresso beans", Category = "coffee", Price = 54.00m },
            new Product { Name = "Filter coffee", Category = "coffee", Price = 39.90m },
            new Product { Name = "Teapot", Category = "accessories", Price = 129.00m },
            new Product { Name = "Dripper", Category = "accessories", Price = 79.00m });
        db.SaveChanges();
    }
}
