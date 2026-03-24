using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TailoredApps.Shared.Email.Models;

namespace TailoredApps.Shared.Email
{
    /// <summary>Implementacja <see cref="IEmailProvider"/> wypisująca wiadomości na konsolę (dev/test).</summary>
    public class EmailServiceToConsolleWritter : IEmailProvider
    {
        /// <inheritdoc/>
        public async Task<ICollection<MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        {
            return new List<MailMessage>();
        }

        /// <inheritdoc/>
        public async Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {
            var message = $"recipent: {recipnet}; topic: {topic}; message: {messageBody}";
            Console.WriteLine(message);
            return message;
        }
    }
}
