using System.Collections.Generic;

namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    /// <summary>
    /// Default implementation of <see cref="IMailMessageBuilder"/> that builds a message by performing
    /// simple key-value token replacement within a named template.
    /// </summary>
    public class DefaultMessageBuilder : IMailMessageBuilder
    {
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
