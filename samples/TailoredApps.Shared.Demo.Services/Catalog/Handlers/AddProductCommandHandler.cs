using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Responses.Commands;
using TailoredApps.Demo.Services.Catalog.Exceptions;
using TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Commands;
using TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;

namespace TailoredApps.Demo.Services.Catalog.Handlers;

public class AddProductCommandHandler(IProductRepository products) : IAddProductCommandHandler
{
    public async Task<AddProductResponse> Handle(AddProduct request, CancellationToken cancellationToken)
    {
        if (await products.FindByNameAsync(request.Name, cancellationToken) is not null)
            throw new ProductAlreadyExistsException(request.Name);

        return new AddProductResponse { Product = await products.AddAsync(request, cancellationToken) };
    }
}
