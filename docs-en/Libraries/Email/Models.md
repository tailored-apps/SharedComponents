# TailoredApps.Shared.Email.Models

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Email.Models)](https://www.nuget.org/packages/TailoredApps.Shared.Email.Models/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

A lightweight package containing only the `MailMessage` data model — a representation of an email message. Separating the model into its own package allows other libraries (e.g. `TailoredApps.Shared.Email.Office365`) to depend only on the model without pulling in the full SMTP implementation.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.Email.Models
```

---

## Przykład użycia

```csharp
using TailoredApps.Shared.Email.Models;

// Przykład: wyświetlenie listy odebranych wiadomości
ICollection<MailMessage> messages = await emailProvider.GetMail(
    folder: "Inbox",
    sender: "boss@company.com",
    fromLast: TimeSpan.FromDays(7)
);

foreach (var msg in messages)
{
    Console.WriteLine($"[{msg.Date:yyyy-MM-dd}] Od: {msg.Sender}");
    Console.WriteLine($"  Temat: {msg.Topic}");
    Console.WriteLine($"  Do:    {msg.Recipent}");

    if (!string.IsNullOrEmpty(msg.HtmlBody))
        Console.WriteLine($"  (HTML body, {msg.HtmlBody.Length} znaków)");

    if (msg.Attachements?.Count > 0)
        Console.WriteLine($"  Załączniki: {string.Join(", ", msg.Attachements.Keys)}");
}
```

---

## API Reference

### Klasa `MailMessage`

| Właściwość | Typ | Opis |
|------------|-----|------|
| `Topic` | `string` | Temat wiadomości |
| `Sender` | `string` | Adres nadawcy |
| `Recipent` | `string` | Adres odbiorcy |
| `Copy` | `string` | Adres CC (kopia) |
| `Body` | `string` | Treść tekstowa (plain-text) |
| `HtmlBody` | `string` | Treść HTML |
| `Attachements` | `Dictionary<string, string>` | Załączniki: nazwa pliku → zawartość Base64 |
| `Date` | `DateTimeOffset` | Data i czas wysłania wiadomości |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.Email.Models — Instrukcja dla agenta AI

Używasz modelu `MailMessage` z biblioteki TailoredApps.Shared.Email.Models.

### Model MailMessage
```csharp
// Właściwości:
msg.Topic       // temat
msg.Sender      // nadawca
msg.Recipent    // odbiorca  
msg.Copy        // CC
msg.Body        // treść plain-text
msg.HtmlBody    // treść HTML
msg.Attachements // Dictionary<string, string> — Base64 załączniki
msg.Date        // DateTimeOffset — data wysłania
```

### Zasady
- Model jest używany jako zwracana wartość przez IEmailProvider.GetMail()
- Załączniki przechowywane jako Base64 — dekoduj przez Convert.FromBase64String() gdy potrzebujesz byte[]
- Właściwość Recipent (nie Recipient) — literówka w API, nie zmieniaj
```
