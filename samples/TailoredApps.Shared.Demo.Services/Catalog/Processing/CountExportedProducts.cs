using System.Runtime.CompilerServices;
using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Model;
using TailoredApps.Demo.Services.Internals;

namespace TailoredApps.Demo.Services.Catalog.Processing;

/// <summary>
/// A stream behavior wraps the sequence a stream handler returns. The request behaviors (logging,
/// validation, caching, fallback, retry) do not apply to streams; what a stream needs is written like this.
/// </summary>
public class CountExportedProducts(ShopMetrics metrics) : IStreamPipelineBehavior<ExportProducts, Product>
{
    public async IAsyncEnumerable<Product> Handle(ExportProducts request, StreamHandlerDelegate<Product> next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var product in next().WithCancellation(cancellationToken))
        {
            metrics.ProductExported();
            yield return product;
        }
    }
}
