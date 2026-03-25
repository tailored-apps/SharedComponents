using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    /// <summary>
    /// Implementation of <see cref="IMailMessageBuilder"/> that builds email message bodies
    /// by loading templates from the file system and replacing <c>{{token}}</c> placeholders
    /// with the provided variable values.
    /// </summary>
    public class TokenReplacingMailMessageBuilder : IMailMessageBuilder
    {
        private readonly IOptions<TokenReplacingMailMessageBuilderOptions> options;

        /// <summary>
        /// Initializes a new instance of <see cref="TokenReplacingMailMessageBuilder"/> with the specified options.
        /// </summary>
        /// <param name="options">
        /// The options that specify the file system location and extension of template files,
        /// wrapped in an <see cref="IOptions{TOptions}"/> accessor.
        /// </param>
        public TokenReplacingMailMessageBuilder(IOptions<TokenReplacingMailMessageBuilderOptions> options)
        {
            this.options = options;
        }

        /// <summary>
        /// Builds an email message body by resolving the named template and replacing all
        /// <c>{{variableName}}</c> placeholders with their corresponding values.
        /// If a file-system location is configured in the options, template files are loaded
        /// from disk and merged into the provided <paramref name="templates"/> dictionary
        /// before the lookup is performed.
        /// </summary>
        /// <param name="templateKey">
        /// The key that identifies the template to use. When templates are loaded from the file system
        /// the key must match the file name (including extension).
        /// </param>
        /// <param name="variables">
        /// A dictionary whose keys are the token names (without <c>{{}}</c> delimiters) and whose values
        /// are the replacement strings to substitute into the template.
        /// </param>
        /// <param name="templates">
        /// An optional dictionary of pre-loaded templates mapping template keys to their raw content.
        /// A <c>null</c> value is treated as an empty dictionary.
        /// </param>
        /// <returns>The resolved template content with all <c>{{token}}</c> placeholders replaced.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when <paramref name="templateKey"/> cannot be found in the resolved templates dictionary.
        /// </exception>
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
