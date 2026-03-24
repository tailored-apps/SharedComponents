using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Email
{
    /// <summary>Interfejs dostawcy e-mail — wysyłanie i odbieranie wiadomości.</summary>
    public interface IEmailProvider
    {
        /// <summary>Wywołanie API.</summary>
        Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments);
        /// <summary>Wywołanie API.</summary>
        Task<ICollection<Models.MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null);

    }
}
