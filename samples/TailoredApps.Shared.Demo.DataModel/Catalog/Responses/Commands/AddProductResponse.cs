using TailoredApps.Demo.DataModel.Catalog.Model;

namespace TailoredApps.Demo.DataModel.Catalog.Responses.Commands;

public class AddProductResponse
{
    public Product Product { get; set; } = new();
}
