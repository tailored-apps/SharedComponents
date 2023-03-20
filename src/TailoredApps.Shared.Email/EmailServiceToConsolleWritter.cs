using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.Email
{
    public class EmailServiceToConsolleWritter : IEmailService
    {
        public void SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {
            Console.WriteLine($"recipent: {recipnet}; topic: {topic}; message: {messageBody}");
        }
    }
}
