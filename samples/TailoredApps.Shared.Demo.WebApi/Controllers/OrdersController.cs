using ForkForward.Mediator;
using Microsoft.AspNetCore.Mvc;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Messages.Queries;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Queries;
using TailoredApps.Shared.ExceptionHandling.Model;

namespace TailoredApps.Demo.WebApi.Controllers;

/// <summary>
/// Orders. No action saves or commits: the transaction filter of EntityFramework.UnitOfWork.WebApiCore
/// commits after a successful action and rolls back after a 4xx/5xx result or an exception.
/// </summary>
[ApiController]
[Route("api/orders")]
public class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateOrder([FromBody] CreateOrder request, CancellationToken cancellationToken)
    {
        var response = await sender.Send(request, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { number = response.Order.Number }, response);
    }

    [HttpGet("{number}")]
    [ProducesResponseType(typeof(GetOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetOrder([FromRoute] string number, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetOrder { Number = number }, cancellationToken));

    /// <summary>Starts a payment for the order and returns the address of the gateway's payment page.</summary>
    [HttpPost("{number}/checkout")]
    [ProducesResponseType(typeof(CheckoutOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> CheckoutOrder([FromRoute] string number, [FromBody] CheckoutOrder request, CancellationToken cancellationToken)
    {
        request.Number = number;
        return Ok(await sender.Send(request, cancellationToken));
    }

    /// <summary>Cancels the order. Repeating it, or cancelling an order that does not exist, succeeds as well.</summary>
    [HttpDelete("{number}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ExceptionHandlingResultModel), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CancelOrder([FromRoute] string number, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelOrder { Number = number }, cancellationToken);
        return NoContent();
    }
}
