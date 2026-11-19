using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Queries;
using TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;

namespace TailoredApps.Demo.Services.Catalog.Handlers;

public class GetProductsQueryHandler(IProductRepository products) : IGetProductsQueryHandler
{
    public async Task<GetProductsResponse> Handle(GetProducts request, CancellationToken cancellationToken)
    {
        var (results, count) = await products.GetProductsAsync(request);
        return new GetProductsResponse { Results = results, Count = count };
    }
}
