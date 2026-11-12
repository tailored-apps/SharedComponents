# TailoredApps.Shared.Email

Sending e-mail behind one interface, `IEmailProvider`. The package contains an SMTP provider, a console provider
for development, and `IMailMessageBuilder` implementations that build a message body from a template and a
dictionary of variables.

## Installation

```bash
dotnet add package TailoredApps.Shared.Email
```

## Registration

```csharp
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;

builder.Services.RegisterSmtpProvider();          // IEmailProvider -> SmtpEmailProvider (transient)
// builder.Services.RegisterConsoleProvider();    // IEmailProvider -> EmailServiceToConsolleWritter (writes to the console)

// Message bodies from template files. No IMailMessageBuilder is registered unless you add one.
builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(options =>
{
    options.Location = Path.Combine(builder.Environment.ContentRootPath, "EmailTemplates");
    options.FileExtension = "html";
});
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
```

## Usage

```csharp
public class WelcomeMailSender(IEmailProvider email, IMailMessageBuilder builder)
{
    // EmailTemplates/welcome.html: <h1>Hello, {{UserName}}!</h1>
    public Task<string> Send(string address, string userName)
    {
        var body = builder.Build("welcome.html", new Dictionary<string, string> { ["UserName"] = userName }, null);

        return email.SendMail(address, "Welcome", body, attachments: null);   // or file name -> byte[]
    }
}
```

## Configuration

`SmtpEmailProvider` reads the `Mail:Providers:Smtp` section:

```json
{
  "Mail": {
    "Providers": {
      "Smtp": {
        "Host": "smtp.example.com",
        "Port": 587,
        "UserName": "noreply@example.com",
        "Password": "<smtp-password>",
        "From": "noreply@example.com",
        "IsProd": true,
        "CatchAll": "developers@example.com"
      }
    }
  }
}
```

| Key | Meaning | Default |
| --- | --- | --- |
| `Host`, `Port` | SMTP server address and port | none, `0` |
| `UserName`, `Password` | Credentials sent to the server | none |
| `From` | Sender address (From and Sender headers); required | none |
| `EnableSsl` | Encrypt the connection | `true` |
| `IsProd` | `true`: send to the given recipient; `false`: send everything to `CatchAll` | `false` |
| `CatchAll` | Address that receives all mail when `IsProd` is `false`; required in that case | none |

`TokenReplacingMailMessageBuilderOptions` is set in code: `Location` (template directory), `FileExtension`
(without the dot; empty means every file) and `HtmlEncodeVariables` (default `true`).

## Good to know

- `IsProd` defaults to `false`: until it is set, every message goes to `CatchAll`, and a missing `CatchAll`
  throws `InvalidOperationException`. A missing `Mail:Providers:Smtp` section throws `InvalidOperationException`
  when the options are first read; a missing `From` throws `ArgumentNullException`.
- `SendMail` takes exactly one recipient. An address containing `,`, `;` or a line break throws `FormatException`.
  It returns the `Message-ID` header it generated for the message.
- Bodies are sent as HTML in UTF-8. `TokenReplacingMailMessageBuilder` HTML-encodes `{{Token}}` values, inserts
  `{{{Token}}}` values as they are, leaves unknown tokens untouched and replaces in a single pass.
- The template key is the file name with its extension. `Location` is searched with its sub-directories and
  read on every `Build` call. An unknown key throws `KeyNotFoundException`.
- `DefaultMessageBuilder` works on the `templates` dictionary passed to `Build` and replaces the variable keys
  as plain text (no braces), HTML-encoding the values unless created with `htmlEncodeVariables: false`.
- `SmtpEmailProvider.GetMail` is not implemented (`NotImplementedException`); the console provider returns an
  empty list. Reading a mailbox is what `TailoredApps.Shared.Email.Office365` does.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/Email/
