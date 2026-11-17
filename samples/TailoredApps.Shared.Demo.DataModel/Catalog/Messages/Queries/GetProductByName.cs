using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;

namespace TailoredApps.Demo.DataModel.Catalog.Messages.Queries;

public class GetProductByName : IRequest<GetProductResponse>
{
    public string Name { get; set; } = string.Empty;
}
