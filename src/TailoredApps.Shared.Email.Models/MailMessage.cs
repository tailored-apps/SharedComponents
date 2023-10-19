using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.Email.Models
{
    public class MailMessage
    {
        public string Topic { get; set; }
        public string Sender { get; set; }
        public string Recipent { get; set; }
        public string Copy { get; set; }
        public string Body { get; set; }
        public string HtmlBody { get; set; }
        public Dictionary<string, string> Attachements { get; set; }
        public DateTimeOffset Date { get; set; }
    }
}
