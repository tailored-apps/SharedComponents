using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.Services.Catalog.Exceptions;
using TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Queries;
using TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;
using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Catalog.Handlers;

public class GetProductQueryHandler(IProductRepository products, HandlerCallCounter counter) : IGetProductQueryHandler
{
    public async Task<GetProductResponse> Handle(GetProduct request, CancellationToken cancellationToken)
    {
        counter.CountGetProduct();
        var product = await products.FindAsync(request.Id, cancellationToken) ?? throw new ProductNotFoundException(request.Id);
        return new GetProductResponse { Product = product };
    }
}
