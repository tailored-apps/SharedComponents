# TailoredApps.Shared.MediatR.Email

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.MediatR.Email)](https://www.nuget.org/packages/TailoredApps.Shared.MediatR.Email/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## 🇵🇱 Opis

Integracja wysyłania emaili z pipeline MediatR. Biblioteka dostarcza command `SendMail` oraz jego handler `SendMailCommandHandler`, dzięki czemu wysyłka emaila staje się naturalną częścią architektury CQRS — możesz wysłać email przez `_mediator.Send(new SendMail { ... })` bez bezpośredniej zależności od `IEmailProvider`.

To podejście umożliwia łatwe wzbogacenie procesu wysyłki o logowanie, retry i auditing z poziomu pipeline behaviors, bez modyfikowania handlera.

## 🇬🇧 Description

MediatR pipeline integration for email sending. The library provides the `SendMail` command and its `SendMailCommandHandler`, making email sending a natural part of CQRS architecture — you can send an email via `_mediator.Send(new SendMail { ... })` without a direct dependency on `IEmailProvider`.

This approach makes it easy to enrich the sending process with logging, retry, and auditing from the pipeline behaviors level, without modifying the handler.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.MediatR.Email
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.Email;
using TailoredApps.Shared.MediatR.Email.Handlers;
using TailoredApps.Shared.Email.MailMessageBuilder;

// 1. Zarejestruj email provider (SMTP lub konsola)
builder.Services.RegisterSmtpProvider();

// 2. Zarejestruj mail message builder
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(o =>
{
    o.Location = "EmailTemplates/";
    o.FileExtension = "html";
});

// 3. Zarejestruj handler
builder.Services.AddTransient<ISendMailCommandHandler, SendMailCommandHandler>();

// 4. MediatR (jeśli jeszcze nie zarejestrowany)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

---

## Przykład użycia

```csharp
public class UserRegistrationService
{
    private readonly IMediator _mediator;

    public UserRegistrationService(IMediator mediator) => _mediator = mediator;

    public async Task RegisterUserAsync(string email, string userName)
    {
        // Logika rejestracji...

        // Wyślij email powitalny przez MediatR pipeline
        var result = await _mediator.Send(new SendMail
        {
            Recipent = email,
            Subject = "Witaj w naszym serwisie!",
            Template = "welcome.html",
            TemplateVariables = new Dictionary<string, string>
            {
                { "UserName", userName },
                { "ActivationUrl", $"https://app.example.com/activate/{Guid.NewGuid()}" }
            },
            Attachments = null
        });

        Console.WriteLine($"Email wysłany, MessageId: {result.MessageId}");
    }

    public async Task SendInvoiceAsync(
        string email,
        string invoiceNumber,
        byte[] pdfContent)
    {
        await _mediator.Send(new SendMail
        {
            Recipent = email,
            Subject = $"Faktura #{invoiceNumber}",
            Template = "invoice.html",
            TemplateVariables = new Dictionary<string, string>
            {
                { "InvoiceNumber", invoiceNumber }
            },
            Attachments = new Dictionary<string, byte[]>
            {
                { $"faktura_{invoiceNumber}.pdf", pdfContent }
            }
        });
    }
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `SendMail` | Command (`IRequest<SendMailResponse>`) | Dane emaila: `Recipent`, `Subject`, `Template`, `TemplateVariables`, `Templates`, `Attachments` |
| `SendMailResponse` | Klasa | Wynik wysyłki: `MessageId` (provider-specific identifier) |
| `SendMailCommandHandler` | Handler | Buduje treść z szablonu i wysyła przez `IEmailProvider` |
| `ISendMailCommandHandler` | Interfejs | Kontrakt handlera |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.MediatR.Email — Instrukcja dla agenta AI

Używasz TailoredApps.Shared.MediatR.Email do wysyłania emaili przez MediatR pipeline.

### Rejestracja
```csharp
builder.Services.RegisterSmtpProvider(); // lub RegisterConsoleProvider()
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
builder.Services.AddTransient<ISendMailCommandHandler, SendMailCommandHandler>();
```

### Wysyłanie emaila
```csharp
var result = await _mediator.Send(new SendMail
{
    Recipent = "user@example.com",
    Subject = "Temat",
    Template = "template.html",           // nazwa pliku szablonu
    TemplateVariables = new() { { "Name", "Jan" } },
    Attachments = null                     // lub Dictionary<string, byte[]>
});
// result.MessageId — ID przypisany przez provider
```

### Zasady
- Template to klucz szablonu przekazywany do IMailMessageBuilder.Build()
- TemplateVariables zastępują {{token}} w szablonie
- Podaj Templates (Dictionary<string,string>) gdy szablony są inline, nie z pliku
- SendMail rzuci jeśli template nie zostanie znaleziony w IMailMessageBuilder
```
