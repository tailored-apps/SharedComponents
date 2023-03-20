using System.Collections.Generic;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    public class DefaultMessageBuilder : IMailMessageBuilder
    {
        public string Build(string templateKey, IDictionary<string, string> variables, IDictionary<string, string> templates)
        {
            if (templates.ContainsKey(templateKey))
            {
                var templateTransform = templates[templateKey];
                foreach (var token in variables)
                {
                    templateTransform = templateTransform.Replace(token.Key, token.Value);
                }
                return templateTransform;
            }
            throw new KeyNotFoundException("templateKey");
        }
    }
}
