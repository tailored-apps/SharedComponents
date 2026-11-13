# TailoredApps.Shared.MediatR.Email

Sending e-mail as a MediatR request. The package contains the `SendMail` command and its handler, which builds
the message body from a template with `IMailMessageBuilder` and sends it through the `IEmailProvider` from
`TailoredApps.Shared.Email`. Application code only needs `IMediator`.

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.Email
```

## Registration

The package has no registration extension of its own. Register a mail provider, a message builder and let
MediatR find the handler in this assembly:

```csharp
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.MediatR.Email.Handlers;

builder.Services.RegisterSmtpProvider();   // or RegisterConsoleProvider()
builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(options =>
{
    options.Location = Path.Combine(builder.Environment.ContentRootPath, "EmailTemplates");
    options.FileExtension = "html";
});
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();

builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(SendMailCommandHandler).Assembly);
});
```

## Usage

```csharp
using TailoredApps.Shared.MediatR.Email.Messages.Commands;

// EmailTemplates/order-confirmation.html: <p>Order {{OrderNumber}} is on its way.</p>
var result = await mediator.Send(new SendMail
{
    Recipent = "customer@example.com",
    Subject = "Your order",
    Template = "order-confirmation.html",
    TemplateVariables = new Dictionary<string, string> { ["OrderNumber"] = "7" },
    Attachments = new Dictionary<string, byte[]> { ["invoice.pdf"] = invoicePdf },   // optional
}, cancellationToken);

// result.MessageId: the identifier returned by IEmailProvider.SendMail
```

## Good to know

- Built on [ForkForward.Mediator](https://www.nuget.org/packages/ForkForward.Mediator), a fork of MediatR 12.5.0 under the Apache-2.0 licence, not on the MediatR package: requests and handlers use the `ForkForward.Mediator` namespace and the mediator is registered with `AddMediator(...)`. "MediatR" in the name of this library is its history, not its dependency.
- The package reads no configuration itself. The SMTP settings (`Mail:Providers:Smtp`) and the template options
  belong to `TailoredApps.Shared.Email`.

- `Template` and `Recipent` are required; the handler throws `ArgumentException` when either is `null`.
  A `null` `Subject` is sent as an empty subject.
- `Template` is the template key handed to `IMailMessageBuilder.Build` together with `TemplateVariables` and
  `Templates`. With `TokenReplacingMailMessageBuilder` it is the template file name including the extension;
  an unknown key throws `KeyNotFoundException`. `Templates` passes templates inline (key -> content).
- MediatR resolves the handler as `IRequestHandler<SendMail, SendMailResponse>`. Registering
  `SendMailCommandHandler` only under `ISendMailCommandHandler` is not enough for `mediator.Send`.
- Delivery follows the registered `IEmailProvider`. With the SMTP provider that means one recipient per message
  and redirection to the `CatchAll` address while `IsProd` is `false`. The Office 365 provider cannot send.
- The cancellation token is not passed on to the mail provider: `IEmailProvider.SendMail` does not take one.
- Safe for trimming and NativeAOT with the SMTP and console providers; there the handler is registered by name:
  `cfg.AddRequestHandler<SendMail, SendMailResponse, SendMailCommandHandler>()`.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/MediatR/Email/
