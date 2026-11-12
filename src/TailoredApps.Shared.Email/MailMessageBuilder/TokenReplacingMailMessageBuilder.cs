using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    /// <summary>
    /// Implementation of <see cref="IMailMessageBuilder"/> that builds email message bodies
    /// by loading templates from the file system and replacing <c>{{token}}</c> placeholders
    /// with the provided variable values.
    /// </summary>
    /// <remarks>
    /// Bodies produced by this builder are sent as HTML. Variable values are therefore HTML-encoded
    /// before insertion (see <see cref="TokenReplacingMailMessageBuilderOptions.HtmlEncodeVariables"/>),
    /// so user-supplied text such as a display name cannot inject links, scripts or markup into a
    /// message sent from a trusted address. Use the triple-brace form <c>{{{token}}}</c> for values that
    /// intentionally contain HTML. Replacement is performed in a single pass, so a value that itself
    /// contains <c>{{other}}</c> is never expanded again.
    /// </remarks>
    public class TokenReplacingMailMessageBuilder : IMailMessageBuilder
    {
        private static readonly Regex TokenPattern = new(@"\{\{\{(?<raw>[A-Za-z0-9_.-]+)\}\}\}|\{\{(?<enc>[A-Za-z0-9_.-]+)\}\}", RegexOptions.Compiled);

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
        /// <c>{{variableName}}</c> placeholders with their (HTML-encoded) values and all
        /// <c>{{{variableName}}}</c> placeholders with their raw values.
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
        /// <returns>The resolved template content with all known placeholders replaced.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when <paramref name="templateKey"/> cannot be found in the resolved templates dictionary.
        /// </exception>
        public string Build(string templateKey, IDictionary<string, string> variables, IDictionary<string, string> templates)
        {
            if (templateKey == null) throw new ArgumentNullException(nameof(templateKey));
            variables ??= new Dictionary<string, string>();
            templates ??= new Dictionary<string, string>();

            var settings = options?.Value;
            if (settings != null && !string.IsNullOrEmpty(settings.Location))
            {
                var extension = string.IsNullOrEmpty(settings.FileExtension) ? "*" : settings.FileExtension;
                var files = new DirectoryInfo(settings.Location).GetFiles($"*.{extension}", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (!templates.ContainsKey(file.Name))
                    {
                        var template = File.ReadAllText(file.FullName);
                        templates.Add(file.Name, template);
                    }
                }
            }

            if (!templates.TryGetValue(templateKey, out var body))
            {
                throw new KeyNotFoundException($"Template '{templateKey}' was not found.");
            }

            var encode = settings?.HtmlEncodeVariables ?? true;
            return TokenPattern.Replace(body, match =>
            {
                var raw = match.Groups["raw"].Success;
                var name = raw ? match.Groups["raw"].Value : match.Groups["enc"].Value;

                if (!variables.TryGetValue(name, out var value))
                {
                    // Unknown tokens are left untouched.
                    return match.Value;
                }

                value ??= string.Empty;
                return raw || !encode ? value : WebUtility.HtmlEncode(value);
            });
        }
    }
}
