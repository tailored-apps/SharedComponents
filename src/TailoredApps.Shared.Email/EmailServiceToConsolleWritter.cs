using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TailoredApps.Shared.Email.Models;

namespace TailoredApps.Shared.Email
{
    /// <summary>
    /// Implementation of <see cref="IEmailProvider"/> that writes email messages to the console output.
    /// Intended for development and testing scenarios where actual email delivery is not required.
    /// </summary>
    public class EmailServiceToConsolleWritter : IEmailProvider
    {
        /// <summary>
        /// Returns an empty collection of mail messages.
        /// This provider does not support retrieving messages and always returns an empty list.
        /// </summary>
        /// <param name="folder">The mailbox folder to retrieve messages from (ignored).</param>
        /// <param name="sender">Filter by sender email address (ignored).</param>
        /// <param name="recipent">Filter by recipient email address (ignored).</param>
        /// <param name="fromLast">Time span to filter messages received within that period (ignored).</param>
        /// <returns>A task that resolves to an empty collection of <see cref="MailMessage"/>.</returns>
        public async Task<ICollection<MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        {
            return new List<MailMessage>();
        }

        /// <summary>
        /// Writes the email details to the standard console output and returns the formatted message string.
        /// No actual email is sent; this method is intended for local development and debugging.
        /// </summary>
        /// <param name="recipnet">The recipient email address.</param>
        /// <param name="topic">The subject line of the email.</param>
        /// <param name="messageBody">The body content of the email.</param>
        /// <param name="attachments">A dictionary of attachment file names mapped to their byte content (not used by this provider).</param>
        /// <returns>
        /// A task that resolves to a formatted string containing the recipient address, topic, and message body
        /// that was written to the console.
        /// </returns>
        public async Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]> attachments)
        {
            var message = $"recipent: {recipnet}; topic: {topic}; message: {messageBody}";
            Console.WriteLine(message);
            return message;
        }
    }
}
