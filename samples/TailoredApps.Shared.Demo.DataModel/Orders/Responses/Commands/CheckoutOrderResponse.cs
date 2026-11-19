namespace TailoredApps.Demo.DataModel.Orders.Responses.Commands;

public class CheckoutOrderResponse
{
    public string Number { get; set; } = string.Empty;

    public string? PaymentId { get; set; }

    /// <summary>The address of the gateway's payment page.</summary>
    public string? RedirectUrl { get; set; }
}
