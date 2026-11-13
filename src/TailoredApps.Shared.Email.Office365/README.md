# TailoredApps.Shared.Email.Office365

An `IEmailProvider` that reads messages from a Microsoft 365 mailbox over IMAP. It signs in as an application
(OAuth2 client credentials, with a client secret or a certificate), so no user password is stored. The provider
reads mail only; it does not send.

## Installation

```bash
dotnet add package TailoredApps.Shared.Email.Office365
```

## Registration

```csharp
using TailoredApps.Shared.Email.Office365;

builder.Services.RegisterOffice365Provider();   // IEmailProvider -> Office365EmailProvider (transient)
```

## Usage

```csharp
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.Models;

public class OrderMailReader(IEmailProvider mailbox)
{
    // Messages in Inbox/Orders from the last 24 hours whose sender contains the given text
    public Task<ICollection<MailMessage>> ReadLastDay()
        => mailbox.GetMail(folder: "Orders", sender: "orders@partner.example", fromLast: TimeSpan.FromHours(24));
}
```

## Configuration

The `Mail:Providers:Office365` section, bound to `AuthenticationConfig`:

```json
{
  "Mail": {
    "Providers": {
      "Office365": {
        "Tenant": "<tenant-id-or-domain>",
        "ClientId": "<application-client-id>",
        "ClientSecret": "<client-secret>",
        "MailBox": "orders@example.com"
      }
    }
  }
}
```

| Key | Meaning | Default |
| --- | --- | --- |
| `Tenant` | Tenant id or a domain of the tenant | none |
| `ClientId` | Application (client) id of the app registration | none |
| `ClientSecret` | Client secret; used when it is set | none |
| `Certificate` | A `CertificateDescription` (Microsoft.Identity.Web); used when there is no client secret | none |
| `MailBox` | Address of the mailbox to read | none |
| `Instance` | Authority format; `{0}` is replaced with `Tenant` | `https://login.microsoftonline.com/{0}` |
| `ApiUrl` | Bound, but not used by the provider | `https://graph.microsoft.com/` |

## Good to know

- `SendMail` is not implemented and throws `NotImplementedException`. Send with the SMTP provider from
  `TailoredApps.Shared.Email`. Both providers register `IEmailProvider`, so when both are registered a single
  injected `IEmailProvider` is the one registered last.
- The provider connects to `outlook.office365.com:993` over TLS and requests a token for
  `https://outlook.office365.com/.default`; neither is configurable. The app registration must be allowed to
  open the mailbox over IMAP as an application.
- `folder` names a sub-folder of the Inbox; empty means the Inbox itself. The folder is opened read-only.
  `sender` and `recipent` are "contains" filters, `fromLast` limits the search to messages delivered after
  now minus that time span. With no filters, every message in the folder is downloaded.
- `GetMail` is asynchronous end to end: connecting, opening the folder, searching and downloading each message are
  awaited, so no thread is blocked while the mailbox answers (from 3.0; before, only connecting was).
- `Office365EmailProvider.GetMail(folder, sender, recipent, fromLast, cancellationToken)` is the cancellable overload; the
  `IEmailProvider` interface has none, so inject the provider class to use it.
- Each matching message is downloaded whole. Attachments are decoded and returned as Base64, keyed by file name;
  a repeated name gets a ` (2)` suffix and a part without a name is called `attachment-N`.
- A missing configuration section throws `InvalidOperationException`; with neither `ClientSecret` nor
  `Certificate` the provider cannot be created; a missing `MailBox` throws `InvalidOperationException` in `GetMail`.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/Email/Office365/
