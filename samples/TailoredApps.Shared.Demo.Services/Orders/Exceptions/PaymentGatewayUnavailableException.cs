using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Orders.Exceptions;

/// <summary>
/// The gateway did not give a usable answer: the payment may or may not exist. The order stays as it was and
/// the caller may try again.
/// </summary>
public class PaymentGatewayUnavailableException(string provider, Exception innerException)
    : ShopException("The payment gateway is not available. Try again in a moment.", 503, innerException)
{
    public string Provider { get; } = provider;
}
