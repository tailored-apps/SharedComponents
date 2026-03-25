namespace TailoredApps.Shared.MediatR.Email.Responses
{
    /// <summary>
    /// Represents the response returned after a <see cref="Messages.Commands.SendMail"/> command is handled.
    /// Contains the provider-assigned identifier for the sent message.
    /// </summary>
    public class SendMailResponse
    {
        /// <summary>
        /// Gets the unique identifier assigned to the sent message by the email provider.
        /// </summary>
        public string MessageId { get; internal set; }
    }
}
