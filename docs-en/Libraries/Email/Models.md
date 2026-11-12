# TailoredApps.Shared.Email.Models

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.Email.Models)](https://www.nuget.org/packages/TailoredApps.Shared.Email.Models/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

A lightweight package containing only the `MailMessage` data model — a representation of an email message. Separating the model into its own package allows other libraries (e.g. `TailoredApps.Shared.Email.Office365`) to depend only on the model without pulling in the full SMTP implementation.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.Email.Models
```

---

## Usage example

```csharp
using TailoredApps.Shared.Email.Models;

// Example: listing the received messages
ICollection<MailMessage> messages = await emailProvider.GetMail(
    folder: "Inbox",
    sender: "boss@company.com",
    fromLast: TimeSpan.FromDays(7)
);

foreach (var msg in messages)
{
    Console.WriteLine($"[{msg.Date:yyyy-MM-dd}] From: {msg.Sender}");
    Console.WriteLine($"  Subject: {msg.Topic}");
    Console.WriteLine($"  To:      {msg.Recipent}");

    if (!string.IsNullOrEmpty(msg.HtmlBody))
        Console.WriteLine($"  (HTML body, {msg.HtmlBody.Length} characters)");

    if (msg.Attachements?.Count > 0)
        Console.WriteLine($"  Attachments: {string.Join(", ", msg.Attachements.Keys)}");
}
```

---

## API Reference

### `MailMessage` class

| Property | Type | Description |
|------------|-----|------|
| `Topic` | `string` | Message subject |
| `Sender` | `string` | Sender address |
| `Recipent` | `string` | Recipient address |
| `Copy` | `string` | CC address (copy) |
| `Body` | `string` | Text body (plain-text) |
| `HtmlBody` | `string` | HTML body |
| `Attachements` | `Dictionary<string, string>` | Attachments: file name → Base64 content |
| `Date` | `DateTimeOffset` | Date and time the message was sent |

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.Email.Models - instructions for an AI agent

You are using the `MailMessage` model from the TailoredApps.Shared.Email.Models library.

### MailMessage model
```csharp
// Properties:
msg.Topic       // subject
msg.Sender      // sender
msg.Recipent    // recipient  
msg.Copy        // CC
msg.Body        // plain-text body
msg.HtmlBody    // HTML body
msg.Attachements // Dictionary<string, string> — Base64 attachments
msg.Date        // DateTimeOffset — date sent
```

### Rules
- The model is used as the return value of IEmailProvider.GetMail()
- Attachments are stored as Base64 — decode with Convert.FromBase64String() when you need byte[]
- The property is Recipent (not Recipient) — a typo in the API, do not change it
````
