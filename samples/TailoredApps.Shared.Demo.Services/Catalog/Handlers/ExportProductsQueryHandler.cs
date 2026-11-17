using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Model;
using TailoredApps.Demo.Services.Catalog.Interfaces.Handlers.Queries;
using TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;

namespace TailoredApps.Demo.Services.Catalog.Handlers;

/// <summary>A stream handler returns the sequence itself; nothing is awaited or buffered here.</summary>
public class ExportProductsQueryHandler(IProductRepository products) : IExportProductsQueryHandler
{
    public IAsyncEnumerable<Product> Handle(ExportProducts request, CancellationToken cancellationToken)
        => products.StreamAsync(request.Category);
}
