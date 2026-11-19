using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TailoredApps.Demo.DataModel.Orders.Model;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Queries;
using TailoredApps.Demo.WebApi;
using TailoredApps.Demo.WebApi.Authentication;
using TailoredApps.Demo.WebApi.Payments;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.Models;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// The sample service, started in memory. Two things differ from a normal run: the clock is fixed, so that
/// times can be asserted exactly, and sent e-mail is recorded instead of printed. The log is recorded as well,
/// next to the usual providers. Everything else, including the fake payment gateways and the in-memory
/// database, is the application's own configuration.
/// </summary>
public class DemoApplication : WebApplicationFactory<Program>
{
    /// <summary>
    /// The time the application sees for the whole test run. The fraction of a second checks that stored
    /// times come back exactly as they were written.
    /// </summary>
    public static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 12, 9, 30, 0, TimeSpan.Zero).AddTicks(1_234_567);

    public Outbox Outbox { get; } = new();

    public LogRecorder Logs { get; } = new();

    public FakeGatewayState Gateways => Services.GetRequiredService<FakeGatewayState>();

    /// <summary>A client whose requests carry a token with these roles, signed with the service's own key.</summary>
    public HttpClient CreateClientAs(params string[] roles)
    {
        var client = CreateClient();
        var token = DevelopmentTokens.Create(Services.GetRequiredService<IOptions<JwtOptions>>().Value, "integration-test", roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            // The last registration wins: IDateTimeProvider, the Revolut signature check and the retry
            // behaviour all read this clock.
            services.AddSingleton<TimeProvider>(new FrozenClock(Now));

            services.AddSingleton(Outbox);
            services.AddTransient<IEmailProvider, RecordingEmailProvider>();
        });
    }
}

/// <summary>
/// A wall clock that does not move. Timers and delays still run in real time (the base class creates them),
/// so retry delays finish as they would in production.
/// </summary>
public sealed class FrozenClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public record SentMessage(string Recipient, string Topic, string Body);

/// <summary>The messages the application sent during a test run.</summary>
public class Outbox
{
    private readonly ConcurrentQueue<SentMessage> messages = new();

    public IReadOnlyCollection<SentMessage> Messages => messages.ToArray();

    public void Add(SentMessage message) => messages.Enqueue(message);
}

/// <summary>Stands in for the console provider of the Email library and keeps what would have been sent.</summary>
public class RecordingEmailProvider(Outbox outbox) : IEmailProvider
{
    public Task<string> SendMail(string recipnet, string topic, string messageBody, Dictionary<string, byte[]>? attachments)
    {
        outbox.Add(new SentMessage(recipnet, topic, messageBody));
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    public Task<ICollection<MailMessage>> GetMail(string folder = "", string sender = "", string recipent = "", TimeSpan? fromLast = null)
        => Task.FromResult<ICollection<MailMessage>>([]);
}

public record LogEntry(string Category, LogLevel Level, string Message);

/// <summary>Everything the application logged, in order, at the levels its configuration lets through.</summary>
public sealed class LogRecorder : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> entries = new();

    public IReadOnlyList<LogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception)));
    }
}

// The shapes of the answers, as a client of the API sees them.

public record ErrorResponse(int ErrorCode, string Message, List<ErrorItem> Errors);

public record ErrorItem(string? Field, string Message);

public static class Answers
{
    /// <summary>The order in the answer to POST /api/orders.</summary>
    public static async Task<Order?> ReadOrderAsync(this HttpContent content)
        => (await content.ReadFromJsonAsync<CreateOrderResponse>())?.Order;

    public static async Task<Order?> GetOrderAsync(this HttpClient client, string number)
        => (await client.GetFromJsonAsync<GetOrderResponse>($"/api/orders/{number}"))?.Order;
}
