using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Services.Internals;
using TailoredApps.Demo.Services.Orders.Entities;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Demo.Services.Orders;

public class OrdersEntitiesMapping : IModelBuilder
{
    public void MapModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Order");
            entity.HasKey(order => order.Id);

            entity.Property(order => order.Id).ValueGeneratedOnAdd();
            entity.Property(order => order.Number).HasMaxLength(32).IsRequired();
            entity.Property(order => order.CustomerEmail).HasMaxLength(254).IsRequired();
            entity.Property(order => order.Amount).HasConversion(SqliteConversions.MinorUnits);
            entity.Property(order => order.CreatedAt).HasConversion(SqliteConversions.UtcTicks);
            entity.Property(order => order.PaidAt).HasConversion(SqliteConversions.UtcTicks);

            entity.HasIndex(order => order.Number).IsUnique();
            entity.HasMany(order => order.Lines).WithOne(line => line.Order).HasForeignKey(line => line.OrderId);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLine");
            entity.HasKey(line => line.Id);

            entity.Property(line => line.Id).ValueGeneratedOnAdd();
            entity.Property(line => line.UnitPrice).HasConversion(SqliteConversions.MinorUnits);

            entity.HasOne(line => line.Product).WithMany().HasForeignKey(line => line.ProductId);
        });
    }
}
