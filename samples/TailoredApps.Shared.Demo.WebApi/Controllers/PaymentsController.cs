using Microsoft.AspNetCore.Mvc;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.WebApi.Controllers;

/// <summary>The payment providers and channels the service can offer: the Payments facade, used directly.</summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController(IPaymentService payments) : ControllerBase
{
    [HttpGet("providers")]
    [ProducesResponseType(typeof(ICollection<PaymentProvider>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetProviders(CancellationToken cancellationToken)
        => Ok(await payments.GetProviders(cancellationToken));

    [HttpGet("providers/{provider}/channels")]
    [ProducesResponseType(typeof(ICollection<PaymentChannel>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetChannels([FromRoute] string provider, [FromQuery] string currency = "PLN", CancellationToken cancellationToken = default)
        => Ok(await payments.GetChannels(provider, currency, cancellationToken));
}
