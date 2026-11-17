using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Catalog.Exceptions;

public class ProductNotFoundException : ShopException
{
    public ProductNotFoundException(int id) : base($"Product {id} does not exist.", 404)
    {
    }

    public ProductNotFoundException(string name) : base($"Product '{name}' does not exist.", 404)
    {
    }
}
