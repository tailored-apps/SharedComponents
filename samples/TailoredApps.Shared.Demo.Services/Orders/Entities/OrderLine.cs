using TailoredApps.Demo.Services.Catalog.Entities;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Demo.Services.Orders.Entities;

public class OrderLine : IModelBase<int>
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>The price at the time of the order; the product's price may change later.</summary>
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
}
