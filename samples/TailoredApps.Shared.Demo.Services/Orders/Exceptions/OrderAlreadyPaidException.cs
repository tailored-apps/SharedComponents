using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Orders.Exceptions;

public class OrderAlreadyPaidException(string number) : ShopException($"Order {number} is already paid and cannot be cancelled.", 409);
