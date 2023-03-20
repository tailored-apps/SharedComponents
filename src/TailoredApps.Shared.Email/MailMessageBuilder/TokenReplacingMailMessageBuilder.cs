using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    public class TokenReplacingMailMessageBuilder : IMailMessageBuilder
    {
        private readonly IOptions<TokenReplacingMailMessageBuilderOptions> options;
        public TokenReplacingMailMessageBuilder(IOptions<TokenReplacingMailMessageBuilderOptions> options)
        {
            this.options = options;
        }
        public string Build(string templateKey, IDictionary<string, string> variables, IDictionary<string, string> templates)
        {
            if (templates == null)
            {
                templates = new Dictionary<string, string>();
            }

            if (options != null && options.Value != null && !string.IsNullOrEmpty(options.Value.Location))
            {
                var files = new DirectoryInfo(options.Value.Location).GetFiles($"*.{options.Value.FileExtension}", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (!templates.ContainsKey(file.Name))
                    {
                        var template = file.OpenText().ReadToEnd();
                        templates.Add(templateKey, template);
                    }
                }
            }

            if (templates.ContainsKey(templateKey))
            {
                var template = templates[templateKey];
                foreach (var key in variables.Keys)
                {
                    template = template.Replace(@"{{" + key + "}}", variables[key]);
                }
                return template;
            }

            throw new KeyNotFoundException("templateKey");
        }

    }
}
