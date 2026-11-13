using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Orders.Exceptions;

/// <summary>The gateway answered, and the answer was no.</summary>
public class PaymentRefusedException() : ShopException("The gateway refused the payment.", 422);
