using System.Text;
using ForkForward.Mediator;
using Microsoft.AspNetCore.Mvc;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.WebApi.Controllers;

/// <summary>
/// One endpoint for the notifications of every gateway. The body is read raw, because gateways sign its
/// exact bytes; what the notification means is decided by the provider, in the handler.
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController(ISender sender) : ControllerBase
{
    [HttpPost("{provider}")]
    [HttpGet("{provider}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReceivePaymentNotification([FromRoute] string provider, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var response = await sender.Send(new ReceivePaymentNotification
        {
            Provider = provider,
            Notification = new PaymentWebhookRequest
            {
                HttpMethod = Request.Method,
                Body = body,
                ContentType = Request.ContentType,
                RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                QueryString = Request.QueryString.Value,
                Headers = Request.Headers.ToDictionary(header => header.Key, header => header.Value),
                Query = Request.Query.ToDictionary(parameter => parameter.Key, parameter => parameter.Value),
            },
        }, cancellationToken);

        // A non-2xx answer makes the gateway send the notification again (and rolls the request back).
        return response.Accepted
            ? Content(response.Acknowledgement, "text/plain")
            : BadRequest(new { reason = response.Rejection });
    }
}
