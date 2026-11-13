# TailoredApps.Shared.Email.Office365

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Email.Office365)](https://www.nuget.org/packages/TailoredApps.Shared.Email.Office365/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Implementacja `IEmailProvider` odczytująca wiadomości e-mail z Microsoft 365 z wykorzystaniem protokołu IMAP i uwierzytelniania OAuth2 (client credentials flow). Biblioteka uwierzytelnia się w Azure AD jako confidential client application — obsługuje zarówno client secret, jak i certyfikat.

Dzięki temu możesz odbierać wiadomości ze skrzynki Office 365 bez przechowywania hasła użytkownika. Biblioteka automatycznie obsługuje pobieranie i cache'owanie tokenów dostępu przez Microsoft Identity.

!!! warning "SendMail — niezaimplementowane"
    Metoda `SendMail` rzuca `NotImplementedException`. Ta biblioteka obsługuje **tylko odbiór** wiadomości przez IMAP. Do wysyłki używaj `SmtpEmailProvider` lub integracji przez Microsoft Graph.

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
        "Tenant": "your-tenant-id-or-domain.onmicrosoft.com",
        "ClientId": "your-application-client-id",
        "MailBox": "shared-mailbox@yourdomain.com",
        "ClientSecret": "your-client-secret"
      }
    }
  }
}
```

`Instance` jest opcjonalne — powyższa wartość to jego wartość domyślna. `AuthenticationConfig` wiąże też `ApiUrl` (domyślnie `https://graph.microsoft.com/`), ale provider go nie używa: zawsze łączy się przez IMAP z `outlook.office365.com:993` i prosi o token dla `https://outlook.office365.com/.default`.

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
            folder: "Orders",                     // podfolder skrzynki odbiorczej (Inbox/Orders)
            sender: "orders@partner.com",
            fromLast: TimeSpan.FromHours(24)
        );

        return messages;
    }

    public async Task<ICollection<MailMessage>> GetAllFromInboxAsync()
    {
        // Pobierz wszystkie wiadomości ze skrzynki odbiorczej (przeczytane i nieprzeczytane)
        return await _emailProvider.GetMail();
    }
}
```

`folder` to nazwa **podfolderu skrzynki odbiorczej (Inbox)**; pusta wartość (domyślna) oznacza samą skrzynkę odbiorczą, więc `folder: "Inbox"` szukałoby `Inbox/Inbox`. Folder jest otwierany tylko do odczytu. `sender` i `recipent` to filtry typu „zawiera”, a `fromLast` ogranicza wynik do wiadomości dostarczonych po chwili „teraz minus ten przedział”; bez filtrów pobierane są wszystkie wiadomości z folderu.

---

## API Reference

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `Office365EmailProvider` | Klasa | Implementacja `IEmailProvider` przez IMAP + OAuth2 |
| `AuthenticationConfig` | Klasa | Konfiguracja Azure AD (tenant, clientId, secret/cert) |
| `AuthenticationConfig.Instance` | Właściwość | URL instancji AAD (domyślnie Azure Public) |
| `AuthenticationConfig.ApiUrl` | Właściwość | Wiązane z konfiguracji (domyślnie `https://graph.microsoft.com/`), ale nieużywane przez provider |
| `AuthenticationConfig.Tenant` | Właściwość | Tenant ID lub domena |
| `AuthenticationConfig.ClientId` | Właściwość | Application ID w Azure AD |
| `AuthenticationConfig.MailBox` | Właściwość | Adres skrzynki do obsługi |
| `AuthenticationConfig.ClientSecret` | Właściwość | Sekret klienta (alternatywa dla certyfikatu) |
| `AuthenticationConfig.Certificate` | Właściwość | Certyfikat (`CertificateDescription`) |
| `Office365EmailProviderExtensions.RegisterOffice365Provider` | Metoda ext. | Rejestruje provider w DI |

---

## 🔒 Bezpieczeństwo i poprawność

- Załączniki w `MailMessage.Attachements` są kluczowane **nazwą pliku** (przy duplikatach z sufiksem `(2)`), a treść jest dekodowana z kodowania transferowego MIME przed zakodowaniem do base64 — wcześniej klucz stanowił typ MIME (kolizja dwóch PDF-ów rzucała wyjątek), a dane były base64-z-base64.
- Uprawnienie `IMAP.AccessAsApp` daje dostęp do **wszystkich** skrzynek w tenancie, o ile nie ograniczy go Application Access Policy w Exchange Online.

---

## 🤖 AI Agent Prompt

````markdown
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
    folder: "Orders",                 // podfolder skrzynki odbiorczej; "" (domyślnie) = sama skrzynka odbiorcza
    sender: "filter@domain.com",
    fromLast: TimeSpan.FromHours(24)
);
```

### Zasady
- SendMail() nie jest zaimplementowane — użyj SmtpEmailProvider do wysyłki
- folder to podfolder skrzynki odbiorczej — nigdy nie podawaj "Inbox"; zostaw pusty, aby czytać skrzynkę odbiorczą
- ApiUrl jest wiązane, ale nieużywane; host IMAP (outlook.office365.com:993) nie jest konfigurowalny
- GetMail jest w pełni asynchroniczne (od 3.0 także otwarcie folderu, wyszukiwanie i pobieranie wiadomości): zawsze używaj await, nigdy .Result ani .Wait(); pobiera całe wiadomości, więc zawężaj zapytanie przez folder, sender i fromLast
- Wymaga uprawnień aplikacyjnych IMAP.AccessAsApp w Azure AD
- Tokeny OAuth2 są automatycznie cache'owane przez MSAL
- Sekcja konfiguracji: "Mail:Providers:Office365"
- Ogranicz aplikację Azure AD polityką Application Access Policy do konkretnych skrzynek
````
