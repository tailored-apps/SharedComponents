using ForkForward.Mediator;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.WebApi.Authentication;
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;

namespace TailoredApps.Demo.WebApi.Endpoints;

/// <summary>
/// The same messages as the controllers, for minimal APIs: the unit of work, validation and exception
/// handling are endpoint filters here (EntityFramework.UnitOfWork.MinimalApi, ExceptionHandling.MinimalApi).
/// The endpoints never save, commit or catch an exception.
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

        products.MapPost("/", async (AddProduct product, ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.Send(product, cancellationToken);
            return Results.Created($"/api/minimal/products/{Uri.EscapeDataString(response.Product.Name)}", response);
        })
            .WithValidation<AddProduct>()
            .RequireAuthorization(policy => policy.RequireRole(Roles.CatalogAdmin));

        products.MapGet("/{name}", (string name, ISender sender, CancellationToken cancellationToken)
            => sender.Send(new GetProductByName { Name = name }, cancellationToken));

        return app;
    }
}
