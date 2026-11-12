using System.Globalization;
using System.Text;
using ForkForward.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Data;
using TailoredApps.Shared.DateTime;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.MediatR.Email.Messages.Commands;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.Orders;

public record CreateOrder(string CustomerEmail, int[] ProductIds);

public record Checkout(string Provider, string? Channel);

public record OrderDto(string Number, string CustomerEmail, decimal Amount, string Currency, string Status, string? PaymentProvider, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt)
{
    public static OrderDto From(Order order)
        => new(order.Number, order.CustomerEmail, order.Amount, order.Currency, order.Status.ToString(), order.PaymentProvider, order.CreatedAt, order.PaidAt);
}

/// <summary>
/// Orders through the unit of work. No action calls SaveChanges: the transaction filter of
/// EntityFramework.UnitOfWork.WebApiCore saves and commits after a successful action and rolls back after a
/// 4xx/5xx result or an exception.
/// </summary>
[ApiController]
[Route("api/orders")]
public class OrdersController(IUnitOfWork<IShopDbContext> unitOfWork, IPaymentService payments, IDateTimeProvider clock) : ControllerBase
{
    private IShopDbContext Db => unitOfWork.DataProvider;

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrder command, CancellationToken cancellationToken)
    {
        var now = clock.OffsetUtcNow;
        var order = new Order
        {
            Number = $"ORD-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..21],
            CustomerEmail = command.CustomerEmail,
            Status = OrderStatus.New,
            CreatedAt = now,
        };
        Db.Orders.Add(order);

        var products = await Db.Products.Where(p => command.ProductIds.Contains(p.Id)).ToListAsync(cancellationToken);
        if (products.Count == 0 || products.Count != command.ProductIds.Distinct().Count())
        {
            // The order was already added to the context. Returning 400 makes the filter roll it back.
            return BadRequest(new { message = "The order has to contain existing products." });
        }

        order.Amount = products.Sum(p => p.Price);
        return Created($"/api/orders/{order.Number}", OrderDto.From(order));
    }

    [HttpGet("{number}")]
    public async Task<ActionResult<OrderDto>> Get(string number, CancellationToken cancellationToken)
    {
        var order = await Db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Number == number, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {number} does not exist.");
        return OrderDto.From(order);
    }

    /// <summary>Starts a payment for the order and returns the address of the gateway's payment page.</summary>
    [HttpPost("{number}/checkout")]
    public async Task<IActionResult> Checkout(string number, Checkout command, CancellationToken cancellationToken)
    {
        var order = await Db.Orders.SingleOrDefaultAsync(o => o.Number == number, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {number} does not exist.");

        PaymentResponse payment;
        try
        {
            payment = await payments.RegisterPayment(new PaymentRequest(command.Provider, order.Amount, order.Currency)
            {
                PaymentChannel = command.Channel,
                Title = $"Order {order.Number}",
                Email = order.CustomerEmail,
                AdditionalData = order.Number,   // the gateway sends it back; Tpay and Adyen identify the payment by it
            }, cancellationToken);
        }
        catch (HttpRequestException exception)   // covers PaymentGatewayException
        {
            // The gateway did not give a usable answer: the payment may or may not exist. The order stays as it was.
            return GatewayUnavailable(exception.Message);
        }
        catch (Stripe.StripeException exception)
        {
            // Stripe reports through the exception of its SDK, for an outage and for a refusal alike; the rule
            // the other providers apply before they throw PaymentGatewayException tells the two apart.
            return PaymentGatewayException.IsUnavailable(exception.HttpStatusCode)
                ? GatewayUnavailable(exception.Message)
                : UnprocessableEntity(new { message = "The gateway refused the payment.", detail = exception.Message });
        }

        if (payment.PaymentStatus == PaymentStatusEnum.Rejected)
            return UnprocessableEntity(new { message = "The gateway refused the payment.", detail = payment.ResponseObject?.ToString() });

        order.PaymentProvider = command.Provider;
        order.PaymentId = payment.PaymentUniqueId;
        order.Status = OrderStatus.AwaitingPayment;
        return Ok(new { order.Number, paymentId = payment.PaymentUniqueId, redirectUrl = payment.RedirectUrl });
    }

    private ObjectResult GatewayUnavailable(string detail)
        => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The payment gateway is not available. Try again in a moment.", detail });
}

/// <summary>
/// One endpoint for the notifications of every gateway. The body is read raw, because gateways sign its
/// exact bytes, and the provider decides what the notification means.
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController(IPaymentService payments, IUnitOfWork<IShopDbContext> unitOfWork, IMediator mediator, IDateTimeProvider clock) : ControllerBase
{
    [HttpPost("{provider}")]
    [HttpGet("{provider}")]
    public async Task<IActionResult> Receive(string provider, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var result = await payments.HandleWebhookAsync(provider, new PaymentWebhookRequest
        {
            HttpMethod = Request.Method,
            Body = body,
            ContentType = Request.ContentType,
            RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            QueryString = Request.QueryString.Value,
            Headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value),
            Query = Request.Query.ToDictionary(q => q.Key, q => q.Value),
        }, cancellationToken);

        switch (result)
        {
            case PaymentWebhookRejected rejected:
                // Nothing in a rejected notification can be trusted. A non-2xx answer makes the gateway send it again.
                return BadRequest(new { reason = rejected.Rejection.ToString() });

            case PaymentWebhookProcessed processed:
                await Apply(processed.Payment, cancellationToken);
                return Acknowledge(processed.Payment);

            case PaymentWebhookIgnored ignored:
                return Acknowledge(ignored.Payment);

            default:
                return Ok();
        }
    }

    private async Task Apply(PaymentResponse payment, CancellationToken cancellationToken)
    {
        // Depending on the gateway the notification carries our own reference (the order number) or the payment id.
        var id = payment.PaymentUniqueId;
        var order = await unitOfWork.DataProvider.Orders.SingleOrDefaultAsync(o => o.Number == id || o.PaymentId == id, cancellationToken);
        if (order is null || order.Status == OrderStatus.Paid)
            return;   // unknown payment, or a notification delivered twice

        if (payment.PaymentStatus == PaymentStatusEnum.Finished)
        {
            order.Status = OrderStatus.Paid;
            order.PaidAt = clock.OffsetUtcNow;

            await mediator.Send(new SendMail
            {
                Recipent = order.CustomerEmail,
                Subject = $"Order {order.Number} is paid",
                Template = "order-confirmation.html",
                TemplateVariables = new Dictionary<string, string>
                {
                    ["OrderNumber"] = order.Number,
                    // The template is English; the server's culture must not decide how the customer sees the amount.
                    ["Amount"] = string.Create(CultureInfo.InvariantCulture, $"{order.Amount:0.00} {order.Currency}"),
                },
            }, cancellationToken);
        }
        else if (payment.PaymentStatus == PaymentStatusEnum.Rejected)
        {
            order.Status = OrderStatus.PaymentRejected;
        }
    }

    /// <summary>Answers with the text the gateway expects (Tpay: TRUE, Adyen: [accepted]) when the provider names one.</summary>
    private ContentResult Acknowledge(PaymentResponse? payment)
        => Content(payment?.ResponseObject as string ?? "OK", "text/plain");
}
