using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.Email.Models
{
    /// <summary>Model wiadomości e-mail.</summary>
    public class MailMessage
    {
        /// <summary>Temat wiadomości.</summary>
        public string Topic { get; set; }

        /// <summary>Nadawca wiadomości.</summary>
        public string Sender { get; set; }

        /// <summary>Odbiorca wiadomości.</summary>
        public string Recipent { get; set; }

        /// <summary>Kopia CC wiadomości.</summary>
        public string Copy { get; set; }

        /// <summary>Treść wiadomości (plain text).</summary>
        public string Body { get; set; }

        /// <summary>Treść wiadomości (HTML).</summary>
        public string HtmlBody { get; set; }

        /// <summary>Załączniki: nazwa pliku → zawartość Base64.</summary>
        public Dictionary<string, string> Attachements { get; set; }

        /// <summary>Data wysłania wiadomości.</summary>
        public DateTimeOffset Date { get; set; }
    }
}
