namespace TailoredApps.Shared.Email.MailMessageBuilder
{
    /// <summary>
    /// Configuration options for <see cref="TokenReplacingMailMessageBuilder"/>.
    /// Specifies where template files are stored on the file system and which file extension they use.
    /// </summary>
    public class TokenReplacingMailMessageBuilderOptions
    {
        /// <summary>
        /// Gets or sets the absolute or relative path to the directory that contains email template files.
        /// When this value is set, the builder will load template files from this location at build time.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the file extension (without the leading dot) used to filter template files
        /// within the <see cref="Location"/> directory (e.g., <c>"html"</c> or <c>"txt"</c>).
        /// </summary>
        public string FileExtension { get; set; }

        /// <summary>
        /// When <c>true</c> (default), values inserted through <c>{{token}}</c> placeholders are HTML-encoded
        /// so that user-supplied text cannot inject markup into the HTML message body. Values inserted through
        /// <c>{{{token}}}</c> are always inserted verbatim.
        /// </summary>
        public bool HtmlEncodeVariables { get; set; } = true;
    }
}
