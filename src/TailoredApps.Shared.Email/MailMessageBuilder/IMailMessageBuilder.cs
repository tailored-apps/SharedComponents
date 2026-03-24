using System.Collections.Generic;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    /// <summary>Interfejs budowania treści wiadomości e-mail z szablonu.</summary>
    public interface IMailMessageBuilder
    {
        /// <summary>Buduje treść wiadomości na podstawie klucza szablonu i zmiennych.</summary>
        string Build(string templateKey, IDictionary<string, string> variables, IDictionary<string, string> templates);
    }
}
