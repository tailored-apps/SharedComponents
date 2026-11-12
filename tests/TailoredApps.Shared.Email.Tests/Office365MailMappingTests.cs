using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MailKit.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using MimeKit;
using TailoredApps.Shared.Email.Office365;
using Xunit;

namespace TailoredApps.Shared.Email.Tests;

/// <summary>The parts of <see cref="Office365EmailProvider"/> that need no mailbox: search query, message mapping and setup.</summary>
public class Office365MailMappingTests
{
    private static MimePart Attachment(string fileName, string content)
    {
        var part = new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes(content))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
        };
        if (fileName is not null)
            part.FileName = fileName;
        return part;
    }

    private static MimeMessage Message(params MimePart[] attachments)
    {
        var message = new MimeMessage
        {
            Subject = "Order 7",
            Date = new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.FromHours(1)),
        };
        message.From.Add(new MailboxAddress("Jan Kowalski", "jan@example.com"));
        message.To.Add(new MailboxAddress("Shop", "shop@shop.example"));
        message.To.Add(new MailboxAddress("Archive", "archive@shop.example"));
        message.Cc.Add(new MailboxAddress("Accounting", "accounting@shop.example"));

        var builder = new BodyBuilder { TextBody = "plain text", HtmlBody = "<p>html text</p>" };
        foreach (var attachment in attachments)
            builder.Attachments.Add(attachment);
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static string Decode(string base64) => Encoding.UTF8.GetString(Convert.FromBase64String(base64));

    // ─── Message mapping ─────────────────────────────────────────────────────

    [Fact]
    public void When_Message_Is_Mapped_Should_Copy_Subject_Bodies_Date_And_Addresses()
    {
        // act
        var mail = Office365EmailProvider.ToMailMessage(Message());

        // assert
        Assert.Equal("Order 7", mail.Topic);
        Assert.Equal("plain text", mail.Body);
        Assert.Equal("<p>html text</p>", mail.HtmlBody);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.FromHours(1)), mail.Date);
        Assert.Equal("\"Jan Kowalski\" <jan@example.com>", mail.Sender);
        Assert.Equal("\"Shop\" <shop@shop.example>,\"Archive\" <archive@shop.example>", mail.Recipent);
        Assert.Equal("\"Accounting\" <accounting@shop.example>", mail.Copy);
        Assert.Empty(mail.Attachements);
    }

    [Fact]
    public void When_Message_Has_No_Addresses_Or_Body_Should_Map_Them_As_Empty()
    {
        // act
        var mail = Office365EmailProvider.ToMailMessage(new MimeMessage());

        // assert
        Assert.Equal(string.Empty, mail.Sender);
        Assert.Equal(string.Empty, mail.Recipent);
        Assert.Equal(string.Empty, mail.Copy);
        Assert.Null(mail.Body);
        Assert.Null(mail.HtmlBody);
        Assert.Empty(mail.Attachements);
    }

    [Fact]
    public void When_Message_Has_Attachments_Should_Return_Their_Decoded_Content_As_Base64_By_File_Name()
    {
        // act
        var mail = Office365EmailProvider.ToMailMessage(Message(Attachment("invoice.txt", "invoice 7"), Attachment("terms.txt", "terms")));

        // assert: one layer of base64 over the original bytes, not the MIME transfer encoding encoded again
        Assert.Equal(new[] { "invoice.txt", "terms.txt" }, mail.Attachements.Keys.ToArray());
        Assert.Equal("invoice 7", Decode(mail.Attachements["invoice.txt"]));
        Assert.Equal("terms", Decode(mail.Attachements["terms.txt"]));
    }

    [Fact]
    public void When_Two_Attachments_Share_A_Name_Should_Keep_Both_Under_Distinct_Keys()
    {
        // act
        var mail = Office365EmailProvider.ToMailMessage(Message(Attachment("scan.txt", "first"), Attachment("scan.txt", "second"), Attachment("scan.txt", "third")));

        // assert
        Assert.Equal(new[] { "scan.txt", "scan.txt (2)", "scan.txt (3)" }, mail.Attachements.Keys.ToArray());
        Assert.Equal("first", Decode(mail.Attachements["scan.txt"]));
        Assert.Equal("second", Decode(mail.Attachements["scan.txt (2)"]));
        Assert.Equal("third", Decode(mail.Attachements["scan.txt (3)"]));
    }

    [Fact]
    public void When_Attachment_Has_No_File_Name_Should_Number_It()
    {
        // act
        var mail = Office365EmailProvider.ToMailMessage(Message(Attachment("named.txt", "named"), Attachment(null, "anonymous")));

        // assert
        Assert.Equal(new[] { "named.txt", "attachment-2" }, mail.Attachements.Keys.ToArray());
        Assert.Equal("anonymous", Decode(mail.Attachements["attachment-2"]));
    }

    // ─── Search query ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void When_No_Filter_Is_Given_Should_Search_For_Every_Message(string sender, string recipient)
    {
        var query = Office365EmailProvider.BuildSearchQuery(sender, recipient, null, DateTime.Now);

        Assert.Same(SearchQuery.All, query);
    }

    [Fact]
    public void When_Every_Filter_Is_Given_Should_Combine_Them_With_And()
    {
        // arrange
        var now = new DateTime(2026, 11, 12, 12, 0, 0);

        // act
        var query = Office365EmailProvider.BuildSearchQuery("jan@example.com", "shop@shop.example", TimeSpan.FromDays(2), now);

        // assert: ((ALL AND FROM) AND TO) AND DELIVERED-AFTER
        var withDate = Assert.IsType<BinarySearchQuery>(query);
        Assert.Equal(SearchTerm.And, withDate.Term);
        var date = Assert.IsType<DateSearchQuery>(withDate.Right);
        Assert.Equal(SearchTerm.DeliveredAfter, date.Term);
        Assert.Equal(new DateTime(2026, 11, 10, 12, 0, 0), date.Date);

        var withRecipient = Assert.IsType<BinarySearchQuery>(withDate.Left);
        var to = Assert.IsType<TextSearchQuery>(withRecipient.Right);
        Assert.Equal(SearchTerm.ToContains, to.Term);
        Assert.Equal("shop@shop.example", to.Text);

        var withSender = Assert.IsType<BinarySearchQuery>(withRecipient.Left);
        var from = Assert.IsType<TextSearchQuery>(withSender.Right);
        Assert.Equal(SearchTerm.FromContains, from.Term);
        Assert.Equal("jan@example.com", from.Text);
        Assert.Same(SearchQuery.All, withSender.Left);
    }

    [Fact]
    public void When_Only_The_Recipient_Is_Given_Should_Filter_By_It_Alone()
    {
        var query = Office365EmailProvider.BuildSearchQuery(string.Empty, "shop@shop.example", null, DateTime.Now);

        var binary = Assert.IsType<BinarySearchQuery>(query);
        Assert.Same(SearchQuery.All, binary.Left);
        Assert.Equal(SearchTerm.ToContains, Assert.IsType<TextSearchQuery>(binary.Right).Term);
    }

    // ─── Setup ───────────────────────────────────────────────────────────────

    private static string SelfSignedPfxBase64()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=office365-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx));
    }

    [Fact]
    public void When_A_Certificate_Is_Configured_Instead_Of_A_Secret_Should_Construct_With_It()
    {
        // arrange
        var config = new AuthenticationConfig
        {
            Tenant = "11111111-2222-3333-4444-555555555555",
            ClientId = "99999999-8888-7777-6666-555555555555",
            MailBox = "mailbox@contoso.com",
            Certificate = CertificateDescription.FromBase64Encoded(SelfSignedPfxBase64()),
        };

        // act
        var provider = new Office365EmailProvider(Options.Create(config));

        // assert
        Assert.NotNull(provider);
        Assert.NotNull(config.Certificate.Certificate);
        Assert.True(config.Certificate.Certificate.HasPrivateKey);
    }

    [Fact]
    public void When_Office365_Provider_Is_Registered_Should_Resolve_It_With_Options_Bound_From_Configuration()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Mail:Providers:Office365:Tenant"] = "11111111-2222-3333-4444-555555555555",
            ["Mail:Providers:Office365:ClientId"] = "99999999-8888-7777-6666-555555555555",
            ["Mail:Providers:Office365:ClientSecret"] = "real-looking-client-secret",
            ["Mail:Providers:Office365:MailBox"] = "mailbox@contoso.com",
        }).Build());

        // act
        services.RegisterOffice365Provider();
        using var provider = services.BuildServiceProvider();

        // assert
        Assert.IsType<Office365EmailProvider>(provider.GetRequiredService<IEmailProvider>());
        var options = provider.GetRequiredService<IOptions<AuthenticationConfig>>().Value;
        Assert.Equal("mailbox@contoso.com", options.MailBox);
        Assert.Equal("https://login.microsoftonline.com/11111111-2222-3333-4444-555555555555", options.Authority);
    }
}
