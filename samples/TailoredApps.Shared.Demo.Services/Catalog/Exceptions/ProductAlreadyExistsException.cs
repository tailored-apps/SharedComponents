using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Catalog.Exceptions;

public class ProductAlreadyExistsException(string name) : ShopException($"Product '{name}' already exists.", 409);
