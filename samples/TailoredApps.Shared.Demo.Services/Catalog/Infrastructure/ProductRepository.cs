using ForkForward.Mapper;
using ForkForward.Mapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.Services.Catalog.Entities;
using TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.Querying;
using Model = TailoredApps.Demo.DataModel.Catalog.Model;

namespace TailoredApps.Demo.Services.Catalog.Infrastructure;

/// <summary>
/// Products through the unit of work. Nothing here commits: the host decides when a request's changes become
/// permanent (the transaction filter of the Web API commits after a successful request and rolls back after
/// a failed one).
/// </summary>
public class ProductRepository(IUnitOfWork<IShopDataContext> unitOfWork, IMapper mapper) : IProductRepository
{
    private IShopDataContext Db => unitOfWork.DataProvider;

    public async Task<(ICollection<Model.Product> results, int count)> GetProductsAsync(GetProducts request)
    {
        IQueryable<Product> products = Db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Filter?.NameContains))
            products = products.Where(product => product.Name.Contains(request.Filter.NameContains));
        if (!string.IsNullOrWhiteSpace(request.Filter?.Category))
            products = products.Where(product => product.Category == request.Filter.Category);

        // Sorting by an allow-list of fields and paging with a size limit (EntityFramework); the mapper
        // projects in the database, so the query selects the model's columns and nothing else.
        var page = await products
            .ApplySorting(request, GetProducts.SortableFields)
            .ProjectTo<Model.Product>(mapper.ConfigurationProvider)
            .PagingAsync(request);

        return (page.Results, page.Count);
    }

    public async Task<Model.Product?> FindAsync(int id, CancellationToken cancellationToken)
        => mapper.Map<Model.Product?>(await Db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Id == id, cancellationToken));

    public async Task<Model.Product?> FindByNameAsync(string name, CancellationToken cancellationToken)
        => mapper.Map<Model.Product?>(await Db.Products.AsNoTracking().SingleOrDefaultAsync(product => product.Name == name, cancellationToken));

    public async Task<Model.Product> AddAsync(AddProduct product, CancellationToken cancellationToken)
    {
        var entity = mapper.Map<Product>(product);
        Db.Products.Add(entity);

        // Saved inside the request's transaction to learn the key; still rolled back if the request fails.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return mapper.Map<Model.Product>(entity);
    }

    public IAsyncEnumerable<Model.Product> StreamAsync(string? category)
    {
        IQueryable<Product> products = Db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
            products = products.Where(product => product.Category == category);

        return products
            .OrderBy(product => product.Name)
            .ProjectTo<Model.Product>(mapper.ConfigurationProvider)
            .AsAsyncEnumerable();
    }
}
