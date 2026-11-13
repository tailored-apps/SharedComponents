using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Services.Catalog;
using TailoredApps.Demo.Services.Catalog.Entities;
using TailoredApps.Demo.Services.Orders;
using TailoredApps.Demo.Services.Orders.Entities;

namespace TailoredApps.Demo.Services;

public class ShopDataContext(DbContextOptions<ShopDataContext> options) : DbContext(options), IShopDataContext
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    // Every feature maps its own entities (IModelBuilder of TailoredApps.Shared.EntityFramework).
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        new CatalogEntitiesMapping().MapModel(modelBuilder);
        new OrdersEntitiesMapping().MapModel(modelBuilder);
    }

    public static void Seed(ShopDataContext db)
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
