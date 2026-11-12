using ForkForward.Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TailoredApps.Shared.Email.Models;
using TailoredApps.Shared.Email.Office365;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.Providers;
using TailoredApps.Shared.MediatR.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Infrastructure;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.Infrastructure;

/// <summary>
/// Maps the application's own exceptions to HTTP answers; everything else keeps the defaults of
/// ExceptionHandling (validation failures: 400 with one error per field, unexpected exceptions: 500 with a
/// generic message, or with the real one in the Development environment).
/// </summary>
public class DemoExceptionHandlingProvider(IHostEnvironment environment) : IExceptionHandlingProvider
{
    private readonly DefaultExceptionHandlingProvider defaults = new(environment);

    public ExceptionHandlingResultModel Response(Exception exception)
        => exception.GetBaseException() is KeyNotFoundException notFound
            ? new ExceptionHandlingResultModel(404, notFound.Message, [new ExceptionOrValidationError(string.Empty, notFound.Message)])
            : defaults.Response(exception);

    public ExceptionHandlingResultModel Response(ModelStateDictionary modelState) => defaults.Response(modelState);
}

/// <summary>The payment providers and channels the application can offer.</summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController(IPaymentService payments) : ControllerBase
{
    [HttpGet("providers")]
    public Task<ICollection<PaymentProvider>> Providers(CancellationToken cancellationToken)
        => payments.GetProviders(cancellationToken);

    [HttpGet("providers/{provider}/channels")]
    public Task<ICollection<PaymentChannel>> Channels(string provider, [FromQuery] string currency = "PLN", CancellationToken cancellationToken = default)
        => payments.GetChannels(provider, currency, cancellationToken);
}

/// <summary>
/// The two libraries that need something outside the application: a trained model (MediatR.ML) and an
/// Office 365 mailbox (Email.Office365). Each is wired only when it is configured; otherwise its endpoint
/// says so with 501.
/// </summary>
[ApiController]
[Route("api")]
public class OptionalFeaturesController(IServiceProvider services, IMediator mediator) : ControllerBase
{
    [HttpPost("images/classify")]
    public async Task<IActionResult> Classify(IFormFile image, CancellationToken cancellationToken)
    {
        if (services.GetService<IImageClassificationService>() is null)
            return NotConfigured("Image classification needs a trained model: set ImageClassification:ModelFilePath.");

        await using var stream = image.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var result = await mediator.Send(new ClassifyImage { FileByteArray = buffer.ToArray(), FileName = image.FileName }, cancellationToken);
        return Ok(result.ImagePrediction);
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken)
    {
        if (services.GetService<Office365EmailProvider>() is not { } office365)
            return NotConfigured("Reading the mailbox needs an Azure AD application: set Demo:Office365Inbox and Mail:Providers:Office365.");

        ICollection<MailMessage> messages = await office365.GetMail(fromLast: TimeSpan.FromDays(1));
        return Ok(messages.Select(m => new { m.Sender, m.Topic, m.Date }));
    }

    private ObjectResult NotConfigured(string message)
        => StatusCode(StatusCodes.Status501NotImplemented, new { message });
}
