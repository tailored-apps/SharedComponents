using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using TailoredApps.Shared.Email.Office365;
using Xunit;

namespace TailoredApps.Shared.Email.Tests;

/// <summary>
/// Reading the mailbox, without Azure AD and without a mailbox: the provider gets its access token and its
/// IMAP connection from the test. The IMAP mocks are strict, so a call to a synchronous MailKit method - one
/// that would block a thread for a network round trip - fails the test.
/// </summary>
public class Office365GetMailTests
{
    private static readonly IOptions<AuthenticationConfig> Options = Microsoft.Extensions.Options.Options.Create(
        new AuthenticationConfig { MailBox = "orders@contoso.example", Tenant = "contoso", ClientId = "client" });

    private static MimeMessage Message(string subject) => new(
        [new MailboxAddress("Partner", "partner@demo.example")],
        [new MailboxAddress("Orders", "orders@contoso.example")],
        subject,
        new TextPart("plain") { Text = "body of " + subject });

    private static (Mock<IImapClient> Client, Mock<IMailFolder> Folder) Mailbox(CancellationToken token, params MimeMessage[] messages)
    {
        var uids = Enumerable.Range(1, messages.Length).Select(i => new UniqueId((uint)i)).ToList();

        var folder = new Mock<IMailFolder>(MockBehavior.Strict);
        folder.Setup(f => f.OpenAsync(FolderAccess.ReadOnly, token)).ReturnsAsync(FolderAccess.ReadOnly);
        folder.Setup(f => f.SearchAsync(It.IsAny<SearchQuery>(), token)).ReturnsAsync(uids);
        for (var i = 0; i < messages.Length; i++)
        {
            var message = messages[i];
            folder.Setup(f => f.GetMessageAsync(uids[i], token, null)).ReturnsAsync(message);
        }

        var client = new Mock<IImapClient>(MockBehavior.Strict);
        client.Setup(c => c.ConnectAsync("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect, token)).Returns(Task.CompletedTask);
        client.Setup(c => c.AuthenticateAsync(It.IsAny<SaslMechanism>(), token)).Returns(Task.CompletedTask);
        client.Setup(c => c.DisconnectAsync(true, token)).Returns(Task.CompletedTask);
        client.Setup(c => c.Inbox).Returns(folder.Object);
        client.Setup(c => c.Dispose());
        return (client, folder);
    }

    private static Office365EmailProvider Provider(Mock<IImapClient> client, Func<CancellationToken, Task<string>> token = null)
        => new(Options, token ?? (_ => Task.FromResult("access-token")), () => client.Object);

    [Fact]
    public async Task When_The_Mailbox_Has_Messages_Should_Download_Each_One_Asynchronously()
    {
        var (client, folder) = Mailbox(CancellationToken.None, Message("Order 1"), Message("Order 2"));

        var mail = await Provider(client).GetMail();

        Assert.Equal(["Order 1", "Order 2"], mail.Select(m => m.Topic));
        Assert.All(mail, m => Assert.Contains("partner@demo.example", m.Sender));
        folder.Verify(f => f.OpenAsync(FolderAccess.ReadOnly, CancellationToken.None), Times.Once);
        folder.Verify(f => f.GetMessageAsync(It.IsAny<UniqueId>(), CancellationToken.None, null), Times.Exactly(2));
        client.Verify(c => c.DisconnectAsync(true, CancellationToken.None), Times.Once);
        client.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task When_A_Token_Is_Given_Should_Pass_It_To_The_Token_Request_And_Every_Imap_Call()
    {
        using var cancellation = new CancellationTokenSource();
        var (client, folder) = Mailbox(cancellation.Token, Message("Order 1"));
        CancellationToken seenByTokenRequest = default;

        var mail = await Provider(client, token =>
        {
            seenByTokenRequest = token;
            return Task.FromResult("access-token");
        }).GetMail(string.Empty, string.Empty, string.Empty, null, cancellation.Token);

        // The strict mocks answer only for this exact token, so every call below carried it.
        Assert.Single(mail);
        Assert.Equal(cancellation.Token, seenByTokenRequest);
        client.Verify(c => c.ConnectAsync("outlook.office365.com", 993, SecureSocketOptions.SslOnConnect, cancellation.Token), Times.Once);
        folder.Verify(f => f.SearchAsync(It.IsAny<SearchQuery>(), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task When_A_Folder_Is_Named_Should_Read_That_Subfolder()
    {
        var (client, inbox) = Mailbox(CancellationToken.None);
        var (_, orders) = Mailbox(CancellationToken.None, Message("In the Orders folder"));
        inbox.Setup(f => f.GetSubfolderAsync("Orders", CancellationToken.None)).ReturnsAsync(orders.Object);

        var mail = await Provider(client).GetMail("Orders");

        Assert.Equal("In the Orders folder", Assert.Single(mail).Topic);
        inbox.Verify(f => f.OpenAsync(It.IsAny<FolderAccess>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_Cancelled_While_Downloading_Should_Stop_And_Release_The_Connection()
    {
        using var cancellation = new CancellationTokenSource();
        var (client, folder) = Mailbox(cancellation.Token, Message("Order 1"), Message("Order 2"), Message("Order 3"));
        folder.Setup(f => f.GetMessageAsync(new UniqueId(2), cancellation.Token, null))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<MimeMessage>(cancellation.Token);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Provider(client).GetMail(string.Empty, string.Empty, string.Empty, null, cancellation.Token));

        folder.Verify(f => f.GetMessageAsync(new UniqueId(3), It.IsAny<CancellationToken>(), null), Times.Never);
        client.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task When_The_Mailbox_Is_Not_Configured_Should_Throw_Before_Connecting()
    {
        var client = new Mock<IImapClient>(MockBehavior.Strict);
        var provider = new Office365EmailProvider(
            Microsoft.Extensions.Options.Options.Create(new AuthenticationConfig()),
            _ => Task.FromResult("access-token"),
            () => client.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetMail());

        Assert.Contains("MailBox", exception.Message);
    }
}
