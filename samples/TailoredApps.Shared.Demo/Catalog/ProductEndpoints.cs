using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Data;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;

namespace TailoredApps.Demo.Catalog;

public record NewProduct(string Name, string Category, decimal Price);

public class NewProductValidator : AbstractValidator<NewProduct>
{
    public NewProductValidator()
    {
        RuleFor(p => p.Name).NotEmpty().MaximumLength(100);
        RuleFor(p => p.Category).NotEmpty();
        RuleFor(p => p.Price).GreaterThan(0);
    }
}

/// <summary>
/// The same building blocks as the controllers, for minimal APIs: the unit of work, validation and exception
/// handling are endpoint filters here (EntityFramework.UnitOfWork.MinimalApi, ExceptionHandling.MinimalApi).
/// The endpoints never call SaveChanges and never catch an exception.
/// </summary>
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // Filters run in the order they are added: exception handling is outermost, so it also answers for
        // an exception thrown while the transaction is committed.
        var products = app.MapGroup("/api/minimal/products")
            .WithExceptionHandling()
            .WithUnitOfWorkTransaction();

        products.MapPost("/", async (NewProduct product, IUnitOfWork<IShopDbContext> unitOfWork, CancellationToken cancellationToken) =>
        {
            var db = unitOfWork.DataProvider;
            db.Products.Add(new Product { Name = product.Name, Category = product.Category, Price = product.Price });

            // The product is already in the context. Answering 409 makes the filter roll it back.
            if (await db.Products.AnyAsync(p => p.Name == product.Name, cancellationToken))
                return Results.Conflict(new { message = $"Product '{product.Name}' already exists." });

            return Results.Created($"/api/minimal/products/{Uri.EscapeDataString(product.Name)}", product);
        }).WithValidation<NewProduct>();

        products.MapGet("/{name}", async (string name, IUnitOfWork<IShopDbContext> unitOfWork, CancellationToken cancellationToken) =>
        {
            var product = await unitOfWork.DataProvider.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Name == name, cancellationToken)
                ?? throw new KeyNotFoundException($"Product '{name}' does not exist.");
            return new ProductDto(product.Id, product.Name, product.Category, product.Price);
        });

        return app;
    }
}
