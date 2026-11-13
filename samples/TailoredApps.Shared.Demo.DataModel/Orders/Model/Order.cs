namespace TailoredApps.Demo.DataModel.Orders.Model;

public class Order
{
    /// <summary>The public identifier of the order. It is also the reference sent to the payment gateway.</summary>
    public string Number { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? PaymentProvider { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public List<OrderLine> Lines { get; set; } = [];
}
