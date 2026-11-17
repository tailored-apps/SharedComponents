using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Orders.Exceptions;

public class UnknownProductsException() : ShopException("The order has to contain existing products.", 400);
