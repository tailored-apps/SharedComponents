using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.Services.Catalog.Exceptions;
using TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Queries;
using TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;

namespace TailoredApps.Demo.Services.Catalog.Handlers;

public class GetProductByNameQueryHandler(IProductRepository products) : IGetProductByNameQueryHandler
{
    public async Task<GetProductResponse> Handle(GetProductByName request, CancellationToken cancellationToken)
    {
        var product = await products.FindByNameAsync(request.Name, cancellationToken) ?? throw new ProductNotFoundException(request.Name);
        return new GetProductResponse { Product = product };
    }
}
