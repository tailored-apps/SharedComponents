using System.Collections.Generic;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    public interface IMailMessageBuilder
    {
        string Build(string templateKey, IDictionary<string, string> variables, IDictionary<string, string> templates);
    }
}
