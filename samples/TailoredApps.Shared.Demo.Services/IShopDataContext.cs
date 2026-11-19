using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Services.Catalog.Entities;
using TailoredApps.Demo.Services.Orders.Entities;

namespace TailoredApps.Demo.Services;

/// <summary>What the unit of work exposes to the repositories: the sets, not the whole DbContext.</summary>
public interface IShopDataContext
{
    DbSet<Product> Products { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderLine> OrderLines { get; }
}
