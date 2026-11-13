using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Orders.Exceptions;

public class OrderNotFoundException(string number) : ShopException($"Order {number} does not exist.", 404);
