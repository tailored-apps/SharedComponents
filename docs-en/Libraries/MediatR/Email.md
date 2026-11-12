# TailoredApps.Shared.MediatR.Email

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.Email)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.Email/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

MediatR pipeline integration for email sending. The library provides the `SendMail` command and its `SendMailCommandHandler`, making email sending a natural part of CQRS architecture — you can send an email via `_mediator.Send(new SendMail { ... })` without a direct dependency on `IEmailProvider`.

This approach makes it easy to enrich the sending process with logging, retry, and auditing from the pipeline behaviors level, without modifying the handler.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.MediatR.Email
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.Email;
using TailoredApps.Shared.MediatR.Email.Handlers;
using TailoredApps.Shared.Email.MailMessageBuilder;

// 1. Register the email provider (SMTP or console)
builder.Services.RegisterSmtpProvider();

// 2. Register the mail message builder
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(o =>
{
    o.Location = "EmailTemplates/";
    o.FileExtension = "html";
});

// 3. MediatR: scan the library assembly as well - that is where SendMailCommandHandler lives
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(SendMailCommandHandler).Assembly);
});
```

The package has no registration extension of its own. MediatR resolves the handler as `IRequestHandler<SendMail, SendMailResponse>`, so registering `SendMailCommandHandler` only under `ISendMailCommandHandler` does not let `_mediator.Send(new SendMail { ... })` find it. Either scan the library assembly as above or register the handler under the MediatR interface yourself:

```csharp
builder.Services.AddTransient<IRequestHandler<SendMail, SendMailResponse>, SendMailCommandHandler>();
```

---

## Usage example

```csharp
using TailoredApps.Shared.MediatR.Email.Messages.Commands;

public class UserRegistrationService
{
    private readonly IMediator _mediator;

    public UserRegistrationService(IMediator mediator) => _mediator = mediator;

    public async Task RegisterUserAsync(string email, string userName)
    {
        // Registration logic...

        // Send the welcome email through the MediatR pipeline
        var result = await _mediator.Send(new SendMail
        {
            Recipent = email,
            Subject = "Welcome to our service!",
            Template = "welcome.html",
            TemplateVariables = new Dictionary<string, string>
            {
                { "UserName", userName },
                { "ActivationUrl", $"https://app.example.com/activate/{Guid.NewGuid()}" }
            },
            Attachments = null
        });

        Console.WriteLine($"Email sent, MessageId: {result.MessageId}");
    }

    public async Task SendInvoiceAsync(
        string email,
        string invoiceNumber,
        byte[] pdfContent)
    {
        await _mediator.Send(new SendMail
        {
            Recipent = email,
            Subject = $"Invoice #{invoiceNumber}",
            Template = "invoice.html",
            TemplateVariables = new Dictionary<string, string>
            {
                { "InvoiceNumber", invoiceNumber }
            },
            Attachments = new Dictionary<string, byte[]>
            {
                { $"invoice_{invoiceNumber}.pdf", pdfContent }
            }
        });
    }
}
```

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `SendMail` | Command (`IRequest<SendMailResponse>`) | Email data: `Recipent`, `Subject`, `Template`, `TemplateVariables`, `Templates`, `Attachments` |
| `SendMailResponse` | Class | Send result: `MessageId` (provider-specific identifier) |
| `SendMailCommandHandler` | Handler | Builds the body from a template and sends it through `IEmailProvider` |
| `ISendMailCommandHandler` | Interface | Handler contract |

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.MediatR.Email - instructions for an AI agent

You are using TailoredApps.Shared.MediatR.Email to send emails through the MediatR pipeline.

### Registration
```csharp
builder.Services.RegisterSmtpProvider(); // or RegisterConsoleProvider()
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(SendMailCommandHandler).Assembly); // the SendMail handler
});
```

### Sending an email
```csharp
var result = await _mediator.Send(new SendMail
{
    Recipent = "user@example.com",
    Subject = "Subject",
    Template = "template.html",           // template file name
    TemplateVariables = new() { { "Name", "Jan" } },
    Attachments = null                     // or Dictionary<string, byte[]>
});
// result.MessageId — the ID assigned by the provider
```

### Rules
- MediatR must find the handler as IRequestHandler<SendMail, SendMailResponse>: scan the library assembly; registering it only as ISendMailCommandHandler is not enough
- Template and Recipent are required - the handler throws ArgumentException when either is null
- Template is the template key passed to IMailMessageBuilder.Build()
- TemplateVariables replace {{token}} in the template
- Provide Templates (Dictionary<string,string>) when the templates are inline, not from a file
- SendMail throws if the template is not found in IMailMessageBuilder
````
