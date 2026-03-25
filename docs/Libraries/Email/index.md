# TailoredApps.Shared.Email

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Email)](https://www.nuget.org/packages/TailoredApps.Shared.Email/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## 🇵🇱 Opis

Biblioteka dostarcza kompletną abstrakcję do wysyłania wiadomości e-mail w aplikacjach .NET. Opiera się na interfejsie `IEmailProvider`, który możesz wymieniać w zależności od środowiska — w produkcji używasz `SmtpEmailProvider` (wysyłka przez SMTP), w lokalnym środowisku deweloperskim `EmailServiceToConsoleWriter` (wypisuje do konsoli bez wysyłki).

Biblioteka zawiera też system budowania treści emaili oparty na szablonach (`IMailMessageBuilder`), który obsługuje proste podstawianie tokenów (`DefaultMessageBuilder`) lub szablony ładowane z pliku z placeholderami `{{token}}` (`TokenReplacingMailMessageBuilder`).

Wbudowane zabezpieczenie przed przypadkowym spamem w środowiskach nieprodukcyjnych: kiedy `IsProd = false`, wszystkie emaile trafiają na adres `CatchAll` zamiast do prawdziwych odbiorców.

## 🇬🇧 Description

This library provides a complete abstraction for sending email messages in .NET applications. It is built around the `IEmailProvider` interface, which can be swapped depending on the environment — in production use `SmtpEmailProvider` (sends via SMTP), in local development use `EmailServiceToConsoleWriter` (prints to console without actual delivery).

The library also includes a template-based email body building system (`IMailMessageBuilder`), supporting simple token substitution (`DefaultMessageBuilder`) or file-system templates with `{{token}}` placeholders (`TokenReplacingMailMessageBuilder`).

Built-in safeguard against accidental spam in non-production environments: when `IsProd = false`, all emails are redirected to the `CatchAll` address instead of real recipients.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Email
```

---

## Rejestracja w DI

=== "SMTP (produkcja)"

    ```csharp
    // Program.cs
    using TailoredApps.Shared.Email;

    // Rejestracja SMTP provider
    builder.Services.RegisterSmtpProvider();

    // Opcjonalnie: rejestracja buildera szablonów
    builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
    builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(options =>
    {
        options.Location = Path.Combine(builder.Environment.ContentRootPath, "EmailTemplates");
        options.FileExtension = "html";
    });
    ```

=== "Konsola (development)"

    ```csharp
    // Program.cs
    builder.Services.RegisterConsoleProvider();
    ```

### Konfiguracja `appsettings.json`

```json
{
  "Mail": {
    "Providers": {
      "Smtp": {
        "Host": "smtp.example.com",
        "Port": 587,
        "UserName": "noreply@example.com",
        "Password": "secret",
        "From": "noreply@example.com",
        "EnableSsl": true,
        "IsProd": true,
        "CatchAll": "dev@example.com"
      }
    }
  }
}
```

---

## Przykład użycia

```csharp
public class NotificationService
{
    private readonly IEmailProvider _emailProvider;
    private readonly IMailMessageBuilder _messageBuilder;

    public NotificationService(
        IEmailProvider emailProvider,
        IMailMessageBuilder messageBuilder)
    {
        _emailProvider = emailProvider;
        _messageBuilder = messageBuilder;
    }

    public async Task SendWelcomeEmailAsync(string recipientEmail, string userName)
    {
        var body = _messageBuilder.Build(
            templateKey: "welcome.html",
            variables: new Dictionary<string, string>
            {
                { "UserName", userName },
                { "AppUrl", "https://myapp.example.com" }
            },
            templates: null  // załaduje z pliku, jeśli skonfigurowano Location
        );

        var messageId = await _emailProvider.SendMail(
            recipnet: recipientEmail,
            topic: "Witaj w MyApp!",
            messageBody: body,
            attachments: null
        );

        Console.WriteLine($"Email sent, MessageId: {messageId}");
    }

    public async Task SendInvoiceAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        byte[] pdfBytes)
    {
        await _emailProvider.SendMail(
            recipnet: recipientEmail,
            topic: subject,
            messageBody: htmlBody,
            attachments: new Dictionary<string, byte[]>
            {
                { "faktura.pdf", pdfBytes }
            }
        );
    }
}
```

### Szablon e-mail (welcome.html)

```html
<!DOCTYPE html>
<html>
<body>
  <h1>Witaj, {{UserName}}!</h1>
  <p>Twoje konto zostało założone. <a href="{{AppUrl}}">Kliknij tutaj</a> by się zalogować.</p>
</body>
</html>
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `IEmailProvider` | Interfejs | Główny kontrakt: `SendMail`, `GetMail` |
| `SmtpEmailProvider` | Klasa | Wysyłka przez SMTP; opcje z `SmtpEmailServiceOptions` |
| `EmailServiceToConsoleWriter` | Klasa | Wypisuje dane emaila do konsoli (dev/test) |
| `SmtpEmailServiceOptions` | Klasa | Konfiguracja SMTP: Host, Port, UserName, Password, From, IsProd, CatchAll |
| `IMailMessageBuilder` | Interfejs | Kontrakt: `Build(templateKey, variables, templates)` |
| `DefaultMessageBuilder` | Klasa | Podstawia tokeny w słowniku szablonów |
| `TokenReplacingMailMessageBuilder` | Klasa | Ładuje szablony z systemu plików; placeholdery `{{token}}` |
| `TokenReplacingMailMessageBuilderOptions` | Klasa | `Location` (ścieżka do katalogu szablonów), `FileExtension` |
| `SmtpEmailProviderExtensions.RegisterSmtpProvider` | Metoda ext. | Rejestruje `SmtpEmailProvider` w DI |
| `SmtpEmailProviderExtensions.RegisterConsoleProvider` | Metoda ext. | Rejestruje `EmailServiceToConsoleWriter` w DI |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.Email — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.Email w projekcie .NET.

### Rejestracja
```csharp
// Produkcja (SMTP):
builder.Services.RegisterSmtpProvider();

// Development (konsola):
builder.Services.RegisterConsoleProvider();

// Builder szablonów (opcjonalnie):
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(o => {
    o.Location = "EmailTemplates/";
    o.FileExtension = "html";
});
```

### appsettings.json
```json
"Mail": { "Providers": { "Smtp": {
  "Host": "smtp.host.com", "Port": 587, "UserName": "user",
  "Password": "pass", "From": "no-reply@app.com",
  "EnableSsl": true, "IsProd": true, "CatchAll": "dev@app.com"
}}}
```

### Użycie
```csharp
// Wstrzyknij IEmailProvider + IMailMessageBuilder
var body = _builder.Build("template.html", variables, null);
await _emailProvider.SendMail(email, subject, body, attachments);
```

### Zasady
- Gdy IsProd=false, wszystkie emaile trafiają na CatchAll — nigdy do prawdziwych odbiorców
- Do testów wstrzyknij IEmailProvider jako mock lub użyj RegisterConsoleProvider
- Placeholdery w szablonach TokenReplacing: {{NazwaTokena}}
- Załączniki: słownik fileName → byte[]
```
