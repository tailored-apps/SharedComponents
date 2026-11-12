# TailoredApps.Shared.Email.Models

The `MailMessage` model: one e-mail message as returned by `IEmailProvider.GetMail` in
`TailoredApps.Shared.Email`. It is a separate package so that code which only handles received messages does not
have to reference a mail provider.

## Installation

```bash
dotnet add package TailoredApps.Shared.Email.Models
```

## Registration

Nothing to register: the package contains one model class and no services.

## Usage

```csharp
using TailoredApps.Shared.Email.Models;

public static void Save(MailMessage message, string directory)
{
    Console.WriteLine($"{message.Date:u} {message.Sender}: {message.Topic}");

    if (message.Attachements is not { } attachments)
    {
        return;
    }

    foreach (var (fileName, base64) in attachments)
    {
        // fileName comes from the sender of the message: keep only its last segment
        File.WriteAllBytes(Path.Combine(directory, Path.GetFileName(fileName)), Convert.FromBase64String(base64));
    }
}
```

| Property | Type | Content |
| --- | --- | --- |
| `Topic` | `string?` | Subject |
| `Sender` | `string?` | Sender |
| `Recipent` | `string?` | Recipient |
| `Copy` | `string?` | CC recipient |
| `Body` | `string?` | Plain-text body |
| `HtmlBody` | `string?` | HTML body |
| `Attachements` | `Dictionary<string, string>?` | File name -> content encoded as Base64 |
| `Date` | `DateTimeOffset` | When the message was sent |

## Good to know

- `Recipent` and `Attachements` are spelled this way in the API.
- Attachment content is a Base64 string, not a byte array: decode it with `Convert.FromBase64String`.
- `Sender`, `Recipent` and `Copy` are single strings. The Office 365 provider joins several addresses with commas.
- Every property except `Date` can be `null`.
- The class has the same name as `System.Net.Mail.MailMessage`; a file that imports both namespaces has to
  qualify the type.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/Email/Models/
