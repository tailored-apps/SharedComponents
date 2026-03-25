using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.Email.Models
{
    /// <summary>Represents an e-mail message.</summary>
    public class MailMessage
    {
        /// <summary>The subject of the message.</summary>
        public string Topic { get; set; }

        /// <summary>The sender of the message.</summary>
        public string Sender { get; set; }

        /// <summary>The recipient of the message.</summary>
        public string Recipent { get; set; }

        /// <summary>The CC (carbon copy) recipient of the message.</summary>
        public string Copy { get; set; }

        /// <summary>The plain-text body of the message.</summary>
        public string Body { get; set; }

        /// <summary>The HTML body of the message.</summary>
        public string HtmlBody { get; set; }

        /// <summary>Attachments as a dictionary mapping file name to Base64-encoded content.</summary>
        public Dictionary<string, string> Attachements { get; set; }

        /// <summary>The date and time the message was sent.</summary>
        public DateTimeOffset Date { get; set; }
    }
}
