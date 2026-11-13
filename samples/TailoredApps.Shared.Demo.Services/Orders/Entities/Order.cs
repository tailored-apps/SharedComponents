using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Demo.Services.Orders.Entities;

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

    public List<OrderLine> Lines { get; set; } = [];
}
