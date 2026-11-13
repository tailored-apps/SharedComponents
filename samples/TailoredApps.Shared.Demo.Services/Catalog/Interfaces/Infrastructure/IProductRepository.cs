using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Model;

namespace TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;

public interface IProductRepository
{
    Task<(ICollection<Product> results, int count)> GetProductsAsync(GetProducts request);

    Task<Product?> FindAsync(int id, CancellationToken cancellationToken);

    Task<Product?> FindByNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>Stores the product and returns it with the key the database gave it.</summary>
    Task<Product> AddAsync(AddProduct product, CancellationToken cancellationToken);

    IAsyncEnumerable<Product> StreamAsync(string? category);
}
