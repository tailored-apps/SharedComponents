# TailoredApps.Shared.Email

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Email)](https://www.nuget.org/packages/TailoredApps.Shared.Email/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

This library provides a complete abstraction for sending email messages in .NET applications. It is built around the `IEmailProvider` interface, which can be swapped depending on the environment — in production use `SmtpEmailProvider` (sends via SMTP), in local development use `EmailServiceToConsoleWriter` (prints to console without actual delivery).

The library also includes a template-based email body building system (`IMailMessageBuilder`), supporting simple token substitution (`DefaultMessageBuilder`) or file-system templates with `{{token}}` placeholders (`TokenReplacingMailMessageBuilder`).

Built-in safeguard against accidental spam in non-production environments: when `IsProd = false`, all emails are redirected to the `CatchAll` address instead of real recipients.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Email
```

---

## DI registration

=== "SMTP (production)"

    ```csharp
    // Program.cs
    using TailoredApps.Shared.Email;

    // Register the SMTP provider
    builder.Services.RegisterSmtpProvider();

    // Optional: register the template builder
    builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
    builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(options =>
    {
        options.Location = Path.Combine(builder.Environment.ContentRootPath, "EmailTemplates");
        options.FileExtension = "html";
    });
    ```

=== "Console (development)"

    ```csharp
    // Program.cs
    builder.Services.RegisterConsoleProvider();
    ```

### `appsettings.json` configuration

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

## Usage example

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
            templates: null  // loaded from disk when Location is configured
        );

        var messageId = await _emailProvider.SendMail(
            recipnet: recipientEmail,
            topic: "Welcome to MyApp!",
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
                { "invoice.pdf", pdfBytes }
            }
        );
    }
}
```

### Email template (welcome.html)

```html
<!DOCTYPE html>
<html>
<body>
  <h1>Hello, {{UserName}}!</h1>
  <p>Your account has been created. <a href="{{AppUrl}}">Click here</a> to sign in.</p>
</body>
</html>
```

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `IEmailProvider` | Interface | Main contract: `SendMail`, `GetMail` |
| `SmtpEmailProvider` | Class | Sends via SMTP; options from `SmtpEmailServiceOptions` |
| `EmailServiceToConsoleWriter` | Class | Writes the email data to the console (dev/test) |
| `SmtpEmailServiceOptions` | Class | SMTP configuration: Host, Port, UserName, Password, From, IsProd, CatchAll |
| `IMailMessageBuilder` | Interface | Contract: `Build(templateKey, variables, templates)` |
| `DefaultMessageBuilder` | Class | Substitutes tokens in the template dictionary |
| `TokenReplacingMailMessageBuilder` | Class | Loads templates from the file system; `{{token}}` placeholders |
| `TokenReplacingMailMessageBuilderOptions` | Class | `Location` (template directory path), `FileExtension` |
| `SmtpEmailProviderExtensions.RegisterSmtpProvider` | Ext. method | Registers `SmtpEmailProvider` in DI |
| `SmtpEmailProviderExtensions.RegisterConsoleProvider` | Ext. method | Registers `EmailServiceToConsoleWriter` in DI |

---

## 🔒 Security

- **Template variables are HTML-encoded.** Message bodies are sent as HTML and `{{token}}` values often come from users (display names, free text). `TokenReplacingMailMessageBuilder` and `DefaultMessageBuilder` encode values (`WebUtility.HtmlEncode`), so `<a href="https://evil">` cannot become a phishing link in a message from a trusted sender. Insert values that intentionally contain HTML with triple braces `{{{token}}}`; turn encoding off globally with `TokenReplacingMailMessageBuilderOptions.HtmlEncodeVariables = false` or `new DefaultMessageBuilder(htmlEncodeVariables: false)`.
- **Single-pass replacement.** A value containing `{{OtherToken}}` is never expanded again.
- **Exactly one recipient.** `SmtpEmailProvider.SendMail` accepts one address; a comma- or semicolon-separated list throws `FormatException` (previously `MailAddressCollection.Add` silently added extra recipients).
- **TLS on by default.** `Mail:Providers:Smtp:EnableSsl` now defaults to `true`.
- **`CatchAll` is required outside production.** With `IsProd = false` a missing `CatchAll` throws `InvalidOperationException` instead of sending nowhere.

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.Email — AI agent instructions

You are using the TailoredApps.Shared.Email library in a .NET project.

### Registration
```csharp
// Production (SMTP):
builder.Services.RegisterSmtpProvider();

// Development (console):
builder.Services.RegisterConsoleProvider();

// Template builder (optional):
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

### Usage
```csharp
// Inject IEmailProvider + IMailMessageBuilder
var body = _builder.Build("template.html", variables, null);
await _emailProvider.SendMail(email, subject, body, attachments);
```

### Rules
- When IsProd=false, every email goes to CatchAll — never to real recipients
- For tests inject IEmailProvider as a mock or use RegisterConsoleProvider
- Placeholders in TokenReplacing templates: {{TokenName}}
- The template key (`templateKey`) must equal the template file name including its extension, e.g. `template.html`; an unknown key → `KeyNotFoundException`
- The "Mail:Providers:Smtp" configuration section is required — a missing section throws `InvalidOperationException`
- Attachments: dictionary fileName → byte[]
- {{token}} values are HTML-encoded; insert HTML deliberately through {{{token}}}
- SendMail takes a single address - loop over recipients in the application
- EnableSsl defaults to true; CatchAll is required when IsProd = false
```
