using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Responses.Commands;

namespace TailoredApps.Demo.DataModel.Catalog.Messages.Commands;

public class AddProduct : IRequest<AddProductResponse>
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }
}
