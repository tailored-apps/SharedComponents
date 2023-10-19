using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TailoredApps.Shared.Email.Models;

namespace TailoredApps.Shared.Email
{
    public class EmailServiceToConsolleWritter : IEmailProvider
    {
        public async Task<ICollection<MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        {
            return new List<MailMessage>();
        }

        public async Task SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {
            Console.WriteLine($"recipent: {recipnet}; topic: {topic}; message: {messageBody}");
        }
    }
}
