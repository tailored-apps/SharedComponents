# TailoredApps.Shared.Email.Office365

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Email.Office365)](https://www.nuget.org/packages/TailoredApps.Shared.Email.Office365/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

An `IEmailProvider` implementation that reads email from Microsoft 365 using IMAP with OAuth2 authentication (client credentials flow). The library authenticates with Azure AD as a confidential client application — supporting both client secret and certificate authentication.

This allows you to receive messages from an Office 365 mailbox without storing a user password. The library automatically handles token acquisition and caching via Microsoft Identity.

!!! warning "SendMail — not implemented"
    The `SendMail` method throws `NotImplementedException`. This library supports **reading only** via IMAP. For sending, use `SmtpEmailProvider` or Microsoft Graph integration.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Email.Office365
```

---

## DI registration

```csharp
// Program.cs
using TailoredApps.Shared.Email.Office365;

builder.Services.RegisterOffice365Provider();
```

### `appsettings.json` configuration

```json
{
  "Mail": {
    "Providers": {
      "Office365": {
        "Instance": "https://login.microsoftonline.com/{0}",
        "Tenant": "your-tenant-id-or-domain.onmicrosoft.com",
        "ClientId": "your-application-client-id",
        "MailBox": "shared-mailbox@yourdomain.com",
        "ClientSecret": "your-client-secret"
      }
    }
  }
}
```

`Instance` is optional - the value above is its default. `AuthenticationConfig` also binds `ApiUrl` (default `https://graph.microsoft.com/`), but the provider does not use it: it always connects over IMAP to `outlook.office365.com:993` and requests a token for `https://outlook.office365.com/.default`.

### Azure AD configuration

The application in Azure AD needs **application** permissions (not delegated ones):

- `IMAP.AccessAsApp` — IMAP access as an application
- Administration: `New-ServicePrincipalAllowedToUseApp` for the specific mailbox

---

## Usage example

```csharp
public class MailboxMonitorService
{
    private readonly IEmailProvider _emailProvider;

    public MailboxMonitorService(IEmailProvider emailProvider)
    {
        _emailProvider = emailProvider;
    }

    public async Task<ICollection<MailMessage>> GetNewOrdersAsync()
    {
        // Get the messages from the last 24 hours from a specific sender
        var messages = await _emailProvider.GetMail(
            folder: "Orders",                     // a sub-folder of the Inbox (Inbox/Orders)
            sender: "orders@partner.com",
            fromLast: TimeSpan.FromHours(24)
        );

        return messages;
    }

    public async Task<ICollection<MailMessage>> GetAllFromInboxAsync()
    {
        // Get all messages from the inbox (read and unread)
        return await _emailProvider.GetMail();
    }
}
```

`folder` names a **sub-folder of the Inbox**; an empty value (the default) means the Inbox itself, so `folder: "Inbox"` would look for `Inbox/Inbox`. The folder is opened read-only. `sender` and `recipent` are "contains" filters and `fromLast` limits the result to messages delivered after now minus that time span; without filters every message in the folder is downloaded.

---

## API Reference

| Type | Kind | Description |
|-----|--------|------|
| `Office365EmailProvider` | Class | `IEmailProvider` implementation over IMAP + OAuth2 |
| `AuthenticationConfig` | Class | Azure AD configuration (tenant, clientId, secret/cert) |
| `AuthenticationConfig.Instance` | Property | AAD instance URL (Azure Public by default) |
| `AuthenticationConfig.ApiUrl` | Property | Bound from configuration (default `https://graph.microsoft.com/`) but not used by the provider |
| `AuthenticationConfig.Tenant` | Property | Tenant ID or domain |
| `AuthenticationConfig.ClientId` | Property | Application ID in Azure AD |
| `AuthenticationConfig.MailBox` | Property | Address of the mailbox to work with |
| `AuthenticationConfig.ClientSecret` | Property | Client secret (alternative to the certificate) |
| `AuthenticationConfig.Certificate` | Property | Certificate (`CertificateDescription`) |
| `Office365EmailProviderExtensions.RegisterOffice365Provider` | Ext. method | Registers the provider in DI |

---

## 🔒 Security and correctness

- Attachments in `MailMessage.Attachements` are keyed by **file name** (duplicates get a `(2)` suffix) and the content is decoded from its MIME transfer encoding before being base64-encoded - previously the key was the MIME type (two PDFs threw) and the data was base64-of-base64.
- The `IMAP.AccessAsApp` permission grants access to **every** mailbox in the tenant unless an Exchange Online Application Access Policy restricts it.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.Email.Office365 - instructions for an AI agent

You are using the TailoredApps.Shared.Email.Office365 library in a .NET project.

### Registration
```csharp
builder.Services.RegisterOffice365Provider();
```

### appsettings.json
```json
"Mail": { "Providers": { "Office365": {
  "Tenant": "tenant-id",
  "ClientId": "app-client-id", 
  "MailBox": "mailbox@domain.com",
  "ClientSecret": "secret"
}}}
```

### Usage
```csharp
// Read only — SendMail throws NotImplementedException!
var messages = await _emailProvider.GetMail(
    folder: "Orders",                 // sub-folder of the Inbox; "" (default) = the Inbox itself
    sender: "filter@domain.com",
    fromLast: TimeSpan.FromHours(24)
);
```

### Rules
- SendMail() is not implemented — use SmtpEmailProvider for sending
- folder is a sub-folder of the Inbox - never pass "Inbox"; leave it empty to read the Inbox
- ApiUrl is bound but unused; the IMAP host (outlook.office365.com:993) is not configurable
- Requires the IMAP.AccessAsApp application permission in Azure AD
- OAuth2 tokens are cached automatically by MSAL
- Configuration section: "Mail:Providers:Office365"
- Restrict the Azure AD application to specific mailboxes with an Application Access Policy
````
