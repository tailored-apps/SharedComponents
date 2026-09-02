using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    /// <summary>
    /// Default implementation of <see cref="IMailMessageBuilder"/> that builds a message by performing
    /// simple key-value token replacement within a named template.
    /// </summary>
    /// <remarks>
    /// Variable values are HTML-encoded before insertion by default, because message bodies are sent
    /// as HTML and values often originate from user input. Pass <c>htmlEncodeVariables: false</c> to the
    /// constructor when the caller guarantees the values are already safe HTML. Replacement is done in a
    /// single pass, so a value containing another token is never expanded again.
    /// </remarks>
    public class DefaultMessageBuilder : IMailMessageBuilder
    {
        private readonly bool htmlEncodeVariables;

        /// <summary>
        /// Initializes a builder that HTML-encodes variable values (safe default).
        /// </summary>
        public DefaultMessageBuilder() : this(htmlEncodeVariables: true)
        {
        }

        /// <summary>
        /// Initializes a builder with an explicit choice about HTML-encoding variable values.
        /// </summary>
        /// <param name="htmlEncodeVariables">
        /// <c>true</c> to HTML-encode every value before insertion; <c>false</c> to insert values verbatim.
        /// </param>
        public DefaultMessageBuilder(bool htmlEncodeVariables)
        {
            this.htmlEncodeVariables = htmlEncodeVariables;
        }

        /// <summary>
        /// Builds an email message body by locating the specified template and replacing each variable
        /// token with its corresponding value.
        /// </summary>
        /// <param name="templateKey">
        /// The key that identifies the template to use within the <paramref name="templates"/> dictionary.
        /// </param>
        /// <param name="variables">
        /// A dictionary whose keys are the token strings to be replaced and whose values are the
        /// replacement text to substitute into the template.
        /// </param>
        /// <param name="templates">
        /// A dictionary mapping template keys to their raw template content strings.
        /// </param>
        /// <returns>The template content with all variable tokens replaced by their corresponding values.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">
        /// Thrown when <paramref name="templateKey"/> is not found in the <paramref name="templates"/> dictionary.
        /// </exception>
        public string Build(string templateKey, IDictionary<string, string> variables, IDictionary<string, string> templates)
        {
            if (templateKey == null) throw new ArgumentNullException(nameof(templateKey));
            if (templates == null) throw new ArgumentNullException(nameof(templates));
            variables ??= new Dictionary<string, string>();

            if (!templates.TryGetValue(templateKey, out var body))
            {
                throw new KeyNotFoundException($"Template '{templateKey}' was not found.");
            }

            var keys = variables.Keys.Where(k => !string.IsNullOrEmpty(k)).OrderByDescending(k => k.Length).ToList();
            if (keys.Count == 0)
            {
                return body;
            }

            // Longest keys first so that "UserName" wins over "User"; single pass so replaced text is never rescanned.
            var pattern = string.Join("|", keys.Select(Regex.Escape));
            return Regex.Replace(body, pattern, match =>
            {
                var value = variables[match.Value] ?? string.Empty;
                return htmlEncodeVariables ? WebUtility.HtmlEncode(value) : value;
            });
        }
    }
}
