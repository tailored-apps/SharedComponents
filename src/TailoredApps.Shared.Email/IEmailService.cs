using System.Collections.Generic;

namespace TailoredApps.Shared.Email
{
    public interface IEmailService
    {
        void SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments);
    }
}
