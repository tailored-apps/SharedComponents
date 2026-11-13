using TailoredApps.Demo.DataModel.Orders.Model;

namespace TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;

public interface IOrderRepository
{
    Task<Order?> FindAsync(string number, CancellationToken cancellationToken);

    /// <summary>Finds the order a gateway means: by our own reference (the order number) or by the payment id.</summary>
    Task<Order?> FindByPaymentReferenceAsync(string reference, CancellationToken cancellationToken);

    Task<Order> CreateAsync(string number, string customerEmail, int[] productIds, DateTimeOffset createdAt, CancellationToken cancellationToken);

    Task MarkAwaitingPaymentAsync(string number, string provider, string? paymentId, CancellationToken cancellationToken);

    Task MarkPaidAsync(string number, DateTimeOffset paidAt, CancellationToken cancellationToken);

    Task MarkPaymentRejectedAsync(string number, CancellationToken cancellationToken);

    Task CancelAsync(string number, CancellationToken cancellationToken);
}
