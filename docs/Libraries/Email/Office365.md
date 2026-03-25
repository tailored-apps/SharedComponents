# TailoredApps.Shared.Email.Office365

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Email.Office365)](https://www.nuget.org/packages/TailoredApps.Shared.Email.Office365/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## 🇵🇱 Opis

Implementacja `IEmailProvider` wysyłająca wiadomości e-mail przez Microsoft 365 z wykorzystaniem protokołu IMAP i uwierzytelniania OAuth2 (client credentials flow). Biblioteka uwierzytelnia się w Azure AD jako confidential client application — obsługuje zarówno client secret, jak i certyfikat.

Dzięki temu możesz odbierać wiadomości ze skrzynki Office 365 bez przechowywania hasła użytkownika. Biblioteka automatycznie obsługuje pobieranie i cache'owanie tokenów dostępu przez Microsoft Identity.

!!! warning "SendMail — niezaimplementowane"
    Metoda `SendMail` rzuca `NotImplementedException`. Ta biblioteka obsługuje **tylko odbiór** wiadomości przez IMAP. Do wysyłki używaj `SmtpEmailProvider` lub integracji przez Microsoft Graph.

## 🇬🇧 Description

An `IEmailProvider` implementation that reads email from Microsoft 365 using IMAP with OAuth2 authentication (client credentials flow). The library authenticates with Azure AD as a confidential client application — supporting both client secret and certificate authentication.

This allows you to receive messages from an Office 365 mailbox without storing a user password. The library automatically handles token acquisition and caching via Microsoft Identity.

!!! warning "SendMail — not implemented"
    The `SendMail` method throws `NotImplementedException`. This library supports **reading only** via IMAP. For sending, use `SmtpEmailProvider` or Microsoft Graph integration.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Email.Office365
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.Email.Office365;

builder.Services.RegisterOffice365Provider();
```

### Konfiguracja `appsettings.json`

```json
{
  "Mail": {
    "Providers": {
      "Office365": {
        "Instance": "https://login.microsoftonline.com/{0}",
        "ApiUrl": "https://graph.microsoft.com/",
        "Tenant": "your-tenant-id-or-domain.onmicrosoft.com",
        "ClientId": "your-application-client-id",
        "MailBox": "shared-mailbox@yourdomain.com",
        "ClientSecret": "your-client-secret"
      }
    }
  }
}
```

### Konfiguracja Azure AD

Aplikacja w Azure AD potrzebuje uprawnień **aplikacyjnych** (nie delegowanych):

- `IMAP.AccessAsApp` — dostęp IMAP jako aplikacja
- Administracja: `New-ServicePrincipalAllowedToUseApp` dla konkretnej skrzynki

---

## Przykład użycia

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
        // Pobierz wiadomości z ostatnich 24 godzin od konkretnego nadawcy
        var messages = await _emailProvider.GetMail(
            folder: "Orders",
            sender: "orders@partner.com",
            fromLast: TimeSpan.FromHours(24)
        );

        return messages;
    }

    public async Task<ICollection<MailMessage>> GetUnreadFromInboxAsync()
    {
        // Pobierz wszystkie wiadomości ze skrzynki odbiorczej
        return await _emailProvider.GetMail();
    }
}
```

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `Office365EmailProvider` | Klasa | Implementacja `IEmailProvider` przez IMAP + OAuth2 |
| `AuthenticationConfig` | Klasa | Konfiguracja Azure AD (tenant, clientId, secret/cert) |
| `AuthenticationConfig.Instance` | Właściwość | URL instancji AAD (domyślnie Azure Public) |
| `AuthenticationConfig.Tenant` | Właściwość | Tenant ID lub domena |
| `AuthenticationConfig.ClientId` | Właściwość | Application ID w Azure AD |
| `AuthenticationConfig.MailBox` | Właściwość | Adres skrzynki do obsługi |
| `AuthenticationConfig.ClientSecret` | Właściwość | Sekret klienta (alternatywa dla certyfikatu) |
| `AuthenticationConfig.Certificate` | Właściwość | Certyfikat (`CertificateDescription`) |
| `Office365EmailProviderExtensions.RegisterOffice365Provider` | Metoda ext. | Rejestruje provider w DI |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.Email.Office365 — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.Email.Office365 w projekcie .NET.

### Rejestracja
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

### Użycie
```csharp
// Tylko odczyt — SendMail rzuca NotImplementedException!
var messages = await _emailProvider.GetMail(
    folder: "Inbox",
    sender: "filter@domain.com",
    fromLast: TimeSpan.FromHours(24)
);
```

### Zasady
- SendMail() nie jest zaimplementowane — użyj SmtpEmailProvider do wysyłki
- Wymaga uprawnień aplikacyjnych IMAP.AccessAsApp w Azure AD
- Tokeny OAuth2 są automatycznie cache'owane przez MSAL
- Sekcja konfiguracji: "Mail:Providers:Office365"
```
