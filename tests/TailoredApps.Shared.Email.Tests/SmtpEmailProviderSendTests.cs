using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace TailoredApps.Shared.Email.Tests;

/// <summary>A minimal SMTP server on the loopback interface that records the messages it accepts.</summary>
internal sealed class LoopbackSmtpServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly Task loop;

    public LoopbackSmtpServer()
    {
        listener.Start();

        // On the thread pool, not on the test's synchronization context: xUnit limits how many
        // continuations run there, and a server waiting for a slot stops answering its client. A test that
        // never connects may stop the listener before the first accept; the loop treats that as the end.
        loop = Task.Run(AcceptAsync);
    }

    public sealed record Message(string From, IReadOnlyList<string> Recipients, string Data);

    public ConcurrentQueue<Message> Messages { get; } = new();

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    private async Task AcceptAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopping.Token);
            }
            catch (Exception exception) when (exception is InvalidOperationException or SocketException or OperationCanceledException)
            {
                return;
            }

            using (client)
            {
                try
                {
                    await TalkAsync(client.GetStream());
                }
                catch (Exception exception) when (exception is IOException or OperationCanceledException)
                {
                    // the client went away or the server is stopping; the test asserts on what was recorded
                }
            }
        }
    }

    private async Task TalkAsync(NetworkStream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 localhost ESMTP test");

        string from = null;
        var recipients = new List<string>();
        while (await reader.ReadLineAsync(stopping.Token) is { } line)
        {
            var command = line.ToUpperInvariant();
            if (command.StartsWith("EHLO", StringComparison.Ordinal) || command.StartsWith("HELO", StringComparison.Ordinal))
            {
                // No extensions advertised: the client sends the message without authenticating.
                await writer.WriteLineAsync("250 localhost");
            }
            else if (command.StartsWith("MAIL FROM:", StringComparison.Ordinal))
            {
                from = Address(line);
                await writer.WriteLineAsync("250 OK");
            }
            else if (command.StartsWith("RCPT TO:", StringComparison.Ordinal))
            {
                recipients.Add(Address(line));
                await writer.WriteLineAsync("250 OK");
            }
            else if (command == "DATA")
            {
                await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                var data = new StringBuilder();
                while (await reader.ReadLineAsync(stopping.Token) is { } dataLine && dataLine != ".")
                    data.AppendLine(dataLine);
                Messages.Enqueue(new Message(from, recipients.ToArray(), data.ToString()));
                await writer.WriteLineAsync("250 OK queued");
            }
            else if (command == "QUIT")
            {
                await writer.WriteLineAsync("221 Bye");
                return;
            }
            else
            {
                await writer.WriteLineAsync("250 OK");
            }
        }
    }

    private static string Address(string line)
    {
        var start = line.IndexOf('<') + 1;
        return line[start..line.IndexOf('>', start)];
    }

    public void Dispose()
    {
        // Cancelling first releases a connection that is still open, so stopping never waits for a client.
        stopping.Cancel();
        listener.Stop();
        loop.Wait(TimeSpan.FromSeconds(5));
        stopping.Dispose();
    }
}

/// <summary><see cref="SmtpEmailProvider.SendMail"/> against a real SMTP exchange on the loopback interface.</summary>
public class SmtpEmailProviderSendTests
{
    private static SmtpEmailProvider Provider(LoopbackSmtpServer server, bool isProd, string catchAll = null)
        => new(Options.Create(new SmtpEmailServiceOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            EnableSsl = false,
            UserName = "user",
            Password = "password",
            From = "shop@shop.example",
            IsProd = isProd,
            CatchAll = catchAll,
        }));

    [Fact]
    public async Task When_Running_In_Production_Should_Send_The_Message_To_The_Given_Recipient()
    {
        // arrange
        using var server = new LoopbackSmtpServer();

        // act
        var messageId = await Provider(server, isProd: true).SendMail("customer@example.com", "Order confirmed", "<p>Thank you</p>", null);

        // assert
        var message = Assert.Single(server.Messages);
        Assert.Equal("shop@shop.example", message.From);
        Assert.Equal(new[] { "customer@example.com" }, message.Recipients);
        Assert.Contains("Subject: Order confirmed", message.Data, StringComparison.Ordinal);
        Assert.Contains("text/html", message.Data, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("utf-8", message.Data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task When_Message_Is_Sent_Should_Return_The_Message_Id_It_Was_Sent_With()
    {
        // arrange
        using var server = new LoopbackSmtpServer();

        // act
        var messageId = await Provider(server, isProd: true).SendMail("customer@example.com", "Topic", "body", null);

        // assert
        Assert.Matches("^<[0-9a-f]{32}@shop\\.example>$", messageId);
        Assert.Contains("Message-ID: " + messageId, Assert.Single(server.Messages).Data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task When_Not_In_Production_Should_Redirect_The_Message_To_The_Catch_All_Address()
    {
        // arrange
        using var server = new LoopbackSmtpServer();

        // act
        await Provider(server, isProd: false, catchAll: "developers@shop.example").SendMail("customer@example.com", "Topic", "body", null);

        // assert
        Assert.Equal(new[] { "developers@shop.example" }, Assert.Single(server.Messages).Recipients);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task When_Not_In_Production_And_No_Catch_All_Is_Configured_Should_Throw_Instead_Of_Mailing_The_Customer(string catchAll)
    {
        // arrange
        using var server = new LoopbackSmtpServer();

        // act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Provider(server, isProd: false, catchAll).SendMail("customer@example.com", "Topic", "body", null));

        // assert
        Assert.Contains("CatchAll", exception.Message, StringComparison.Ordinal);
        Assert.Empty(server.Messages);
    }

    [Fact]
    public async Task When_Attachments_Are_Given_Should_Send_Each_Under_Its_Name()
    {
        // arrange
        using var server = new LoopbackSmtpServer();
        var attachments = new Dictionary<string, byte[]>
        {
            ["invoice.txt"] = Encoding.UTF8.GetBytes("invoice 7"),
            ["terms.txt"] = Encoding.UTF8.GetBytes("terms and conditions"),
        };

        // act
        await Provider(server, isProd: true).SendMail("customer@example.com", "Invoice", "see attachments", attachments);

        // assert
        var data = Assert.Single(server.Messages).Data;
        Assert.Contains("invoice.txt", data, StringComparison.Ordinal);
        Assert.Contains("terms.txt", data, StringComparison.Ordinal);
        Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("invoice 7")), data, StringComparison.Ordinal);
        Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("terms and conditions")), data, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task When_Recipient_Is_Blank_Should_Throw_ArgumentException_Without_Sending(string recipient)
    {
        using var server = new LoopbackSmtpServer();

        await Assert.ThrowsAsync<ArgumentException>(() => Provider(server, isProd: true).SendMail(recipient, "Topic", "body", null));

        Assert.Empty(server.Messages);
    }

    [Theory]
    [InlineData("customer@example.com,attacker@example.net")]
    [InlineData("customer@example.com;attacker@example.net")]
    [InlineData("customer@example.com\r\nBcc: attacker@example.net")]
    public async Task When_Recipient_Is_A_List_Should_Throw_FormatException_Without_Sending(string recipient)
    {
        using var server = new LoopbackSmtpServer();

        await Assert.ThrowsAsync<FormatException>(() => Provider(server, isProd: true).SendMail(recipient, "Topic", "body", null));

        Assert.Empty(server.Messages);
    }

    private static ServiceProvider Services(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Mail:Providers:Smtp:Host"] = "smtp.shop.example",
            ["Mail:Providers:Smtp:Port"] = "2525",
            ["Mail:Providers:Smtp:From"] = "shop@shop.example",
            ["Mail:Providers:Smtp:CatchAll"] = "developers@shop.example",
        }).Build());
        register(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void When_Smtp_Provider_Is_Registered_Should_Resolve_It_With_Options_Bound_From_Configuration()
    {
        using var provider = Services(services => services.RegisterSmtpProvider());

        Assert.IsType<SmtpEmailProvider>(provider.GetRequiredService<IEmailProvider>());
        var options = provider.GetRequiredService<IOptions<SmtpEmailServiceOptions>>().Value;
        Assert.Equal("smtp.shop.example", options.Host);
        Assert.Equal(2525, options.Port);
        Assert.Equal("developers@shop.example", options.CatchAll);
    }

    [Fact]
    public void When_Console_Provider_Is_Registered_Should_Resolve_The_Console_Writer()
    {
        using var provider = Services(services => services.RegisterConsoleProvider());

        Assert.IsType<EmailServiceToConsolleWritter>(provider.GetRequiredService<IEmailProvider>());
        Assert.Equal("smtp.shop.example", provider.GetRequiredService<IOptions<SmtpEmailServiceOptions>>().Value.Host);
    }
}
