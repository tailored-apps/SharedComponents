using ForkForward.Mediator;
using Microsoft.AspNetCore.Mvc;
using TailoredApps.Shared.Email.Models;
using TailoredApps.Shared.Email.Office365;
using TailoredApps.Shared.Mediator.ImageClassification.Domain.DataModel.Message.Commands;
using TailoredApps.Shared.Mediator.ImageClassification.Interfaces.Infrastructure;

namespace TailoredApps.Demo.WebApi.Controllers;

/// <summary>
/// The two libraries that need something outside the service: a trained model (Mediator.ML) and an
/// Office 365 mailbox (Email.Office365). Each is wired only when it is configured; otherwise its endpoint
/// says so with 501.
/// </summary>
[ApiController]
[Route("api")]
public class OptionalFeaturesController(IServiceProvider services, ISender sender) : ControllerBase
{
    [HttpPost("images/classify")]
    public async Task<IActionResult> Classify(IFormFile image, CancellationToken cancellationToken)
    {
        if (services.GetService<IImageClassificationService>() is null)
            return NotConfigured("Image classification needs a trained model: set ImageClassification:ModelFilePath.");

        await using var stream = image.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var result = await sender.Send(new ClassifyImage { FileByteArray = buffer.ToArray(), FileName = image.FileName }, cancellationToken);
        return Ok(result.ImagePrediction);
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken)
    {
        if (services.GetService<Office365EmailProvider>() is not { } office365)
            return NotConfigured("Reading the mailbox needs an Azure AD application: set Demo:Office365Inbox and Mail:Providers:Office365.");

        ICollection<MailMessage> messages = await office365.GetMail(fromLast: TimeSpan.FromDays(1));
        return Ok(messages.Select(message => new { message.Sender, message.Topic, message.Date }));
    }

    private ObjectResult NotConfigured(string message)
        => StatusCode(StatusCodes.Status501NotImplemented, new { message });
}
