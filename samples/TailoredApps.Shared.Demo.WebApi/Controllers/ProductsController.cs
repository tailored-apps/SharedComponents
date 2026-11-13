using ForkForward.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Messages.Queries;
using TailoredApps.Demo.DataModel.Catalog.Model;
using TailoredApps.Demo.DataModel.Catalog.Responses.Commands;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.WebApi.Authentication;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Demo.WebApi.Controllers;

/// <summary>
/// A controller only translates HTTP to a message and back. What happens is in the handler; validation,
/// caching, retries, the transaction and the error response are around it, not in it.
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/products?page=1&amp;count=3&amp;sortField=Price&amp;sortDir=Desc&amp;filter.category=tea</summary>
    [HttpGet]
    [ProducesResponseType(typeof(GetProductsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetProducts([FromQuery] GetProducts request, CancellationToken cancellationToken)
        => Ok(await sender.Send(request, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GetProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetProduct([FromRoute] int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetProduct { Id = id }, cancellationToken));

    [HttpGet("{id:int}/recommendations")]
    [ProducesResponseType(typeof(GetRecommendationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetRecommendations([FromRoute] int id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetRecommendations { ProductId = id }, cancellationToken));

    /// <summary>The whole catalogue as a stream: each product is written to the response as the database returns it.</summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
    public IAsyncEnumerable<Product> ExportProducts([FromQuery] ExportProducts request, CancellationToken cancellationToken)
        => sender.CreateStream(request, cancellationToken);

    [Authorize(Roles = Roles.CatalogAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(AddProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AddProduct([FromBody] AddProduct request, CancellationToken cancellationToken)
    {
        var response = await sender.Send(request, cancellationToken);
        return CreatedAtAction(nameof(GetProduct), new { id = response.Product.Id }, response);
    }
}
