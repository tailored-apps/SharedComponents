namespace TailoredApps.Demo.Services.Orders.Entities;

public enum OrderStatus
{
    New,
    AwaitingPayment,
    Paid,
    PaymentRejected,
    Cancelled,
}
