using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.Services.Orders.Exceptions;
using TailoredApps.Demo.Services.Orders.Interfaces.Handlers.Commands;
using TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.Services.Orders.Handlers;

/// <summary>Starts a payment for the order and returns the address of the gateway's payment page.</summary>
public class CheckoutOrderCommandHandler(IOrderRepository orders, IPaymentService payments) : ICheckoutOrderCommandHandler
{
    public async Task<CheckoutOrderResponse> Handle(CheckoutOrder request, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(request.Number, cancellationToken) ?? throw new OrderNotFoundException(request.Number);

        PaymentResponse payment;
        try
        {
            payment = await payments.RegisterPayment(new PaymentRequest(request.Provider, order.Amount, order.Currency)
            {
                PaymentChannel = request.Channel,
                Title = $"Order {order.Number}",
                Email = order.CustomerEmail,
                AdditionalData = order.Number,   // the gateway sends it back; Tpay and Adyen identify the payment by it
            }, cancellationToken);
        }
        catch (HttpRequestException exception)   // PaymentGatewayException, from every provider, or no connection
        {
            throw new PaymentGatewayUnavailableException(request.Provider, exception);
        }

        if (payment.PaymentStatus == PaymentStatusEnum.Rejected)
            throw new PaymentRefusedException();

        await orders.MarkAwaitingPaymentAsync(order.Number, request.Provider, payment.PaymentUniqueId, cancellationToken);
        return new CheckoutOrderResponse { Number = order.Number, PaymentId = payment.PaymentUniqueId, RedirectUrl = payment.RedirectUrl };
    }
}
