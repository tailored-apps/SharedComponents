using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Email
{
    public interface IEmailProvider
    {
        Task SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments);
        Task<ICollection<Models.MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "");

    }
}
