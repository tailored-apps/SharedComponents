using System.Text.Json.Serialization;
using FluentValidation;
using ForkForward.Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.MediatR.DI;
using TailoredApps.Shared.MediatR.Email.Handlers;
using TailoredApps.Shared.MediatR.Email.Messages.Commands;
using TailoredApps.Shared.MediatR.Email.Responses;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PagedRequest;
using TailoredApps.Shared.Querying;

namespace TailoredApps.AotSmoke;

/// <summary>
/// The MediatR libraries the way a NativeAOT application uses them: handlers, fallback handlers and retry
/// settings are registered by name instead of being found by scanning, and the behaviors are registered closed
/// per request (the container cannot close an open generic over a value type natively).
/// The response cache is checked by its own application, TailoredApps.Shared.AotSmoke.Caching: HybridCache is
/// the one dependency that still reports trim warnings, and this project allows none.
/// </summary>
internal static class MediatorChecks
{
    public static async Task RunAsync(Action<string, bool, string?> check)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Counters>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddMediator(cfg =>
        {
            cfg.AddRequestHandler<GetGreeting, string, GetGreetingHandler>();
            cfg.AddRequestHandler<CountItems, int, CountItemsHandler>();
            cfg.AddRequestHandler<Touch, TouchHandler>();
            cfg.AddRequestHandler<GetQuote, string, GetQuoteHandler>();
            cfg.AddRequestHandler<FindItems, ItemPage, FindItemsHandler>();
            cfg.AddRequestHandler<SendMail, SendMailResponse, SendMailCommandHandler>();   // MediatR.Email
        });

        new PipelineRegistration(services)
            .AddPipelineBehaviorsWithoutCaching<GetGreeting, string>()
            .AddPipelineBehaviorsWithoutCaching<CountItems, int>()          // a value-type response
            .AddPipelineBehaviorsWithoutCaching<GetQuote, string>()
            .AddPipelineBehaviorsWithoutCaching<FindItems, ItemPage>()
            .AddPipelineBehaviorsWithoutCaching<SendMail, SendMailResponse>()
            .AddFallbackHandler<GetQuote, string, GetQuoteFallback>()
            .AddRetryPolicy<GetQuote, string, GetQuoteRetry>();

        services.AddTransient<IValidator<GetGreeting>, GetGreetingValidator>();
        services.RegisterConsoleProvider();
        services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var counters = provider.GetRequiredService<Counters>();

        async Task Run(string name, Func<Task<(bool, string?)>> body)
        {
            try
            {
                var (passed, detail) = await body();
                check(name, passed, detail);
            }
            catch (Exception exception)
            {
                check(name, false, $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        await Run("mediator: a request reaches its handler through the behaviors", async () =>
        {
            var greeting = await mediator.Send(new GetGreeting("Anna"));
            return (greeting == "Hello, Anna!", greeting);
        });

        await Run("mediator: validation refuses an invalid request", async () =>
        {
            try
            {
                await mediator.Send(new GetGreeting(string.Empty));
                return (false, "no exception");
            }
            catch (ValidationException exception)
            {
                return (exception.Errors.Any(e => e.PropertyName == nameof(GetGreeting.Name)), exception.Message);
            }
        });

        await Run("mediator: a value-type response goes through the closed behaviors", async () =>
        {
            var count = await mediator.Send(new CountItems("tea"));
            return (count == 3 && counters.CountItems == 1, $"{count}, handler calls {counters.CountItems}");
        });

        // The behaviors are for requests with a response (IRequest<TResponse>); this one goes straight to its handler.
        await Run("mediator: a request without a response runs", async () =>
        {
            await mediator.Send(new Touch());
            return (counters.Touch == 1, counters.Touch.ToString());
        });

        await Run("mediator: a failing handler is retried, then the fallback answers", async () =>
        {
            var quote = await mediator.Send(new GetQuote());
            return (quote == "fallback" && counters.GetQuote == 3, $"{quote} after {counters.GetQuote} attempts");
        });

        await Run("mediator: a paged request carries paging, sorting and its filter", async () =>
        {
            var page = await mediator.Send(new FindItems { Page = 2, Count = 10, SortField = "Name", SortDir = SortDirection.Desc, Filter = new ItemFilter { Category = "tea" } });
            string[] expected = ["page 2", "count 10", "sorted by name: True Desc", "category tea"];
            return (page.Count == 42 && page.Results.SequenceEqual(expected), string.Join("; ", page.Results));
        });

        await Run("mediator: SendMail renders the template and sends it", async () =>
        {
            var sent = await mediator.Send(new SendMail
            {
                Recipent = "anna@demo.example",
                Subject = "Order 42",
                Template = "order.html",
                TemplateVariables = new Dictionary<string, string> { ["Number"] = "42" },
                Templates = new Dictionary<string, string> { ["order.html"] = "<p>Order {{Number}} is paid.</p>" },
            });
            return (sent.MessageId?.Contains("Order 42 is paid.", StringComparison.Ordinal) == true, sent.MessageId);
        });

        // Without a JIT a content-based key cannot be built by reflection; the library says so instead of
        // hashing whatever members survived trimming.
        try
        {
            CacheKeyGenerator.Generate(typeof(CountItems), new CountItems("tea"));
            check("mediator: the reflection cache key is refused without reflection-based JSON", false, "no exception");
        }
        catch (InvalidOperationException exception)
        {
            check("mediator: the reflection cache key is refused without reflection-based JSON", exception.Message.Contains("GetCacheKey", StringComparison.Ordinal), exception.Message);
        }

        var generated = CacheKeyGenerator.Generate(new CountItems("tea"), MediatorJsonContext.Default.CountItems);
        check("mediator: a cache key from generated JSON metadata", generated.StartsWith("TailoredApps.AotSmoke.CountItems:", StringComparison.Ordinal) && generated.Length > 60, generated);
    }
}

internal sealed class Counters
{
    public int CountItems;
    public int Touch;
    public int GetQuote;
}

public record GetGreeting(string Name) : IRequest<string>;

public sealed class GetGreetingValidator : AbstractValidator<GetGreeting>
{
    public GetGreetingValidator() => RuleFor(r => r.Name).NotEmpty();
}

internal sealed class GetGreetingHandler : IRequestHandler<GetGreeting, string>
{
    public Task<string> Handle(GetGreeting request, CancellationToken cancellationToken) => Task.FromResult($"Hello, {request.Name}!");
}

public record CountItems(string Category) : IRequest<int>;

internal sealed class CountItemsHandler(Counters counters) : IRequestHandler<CountItems, int>
{
    public Task<int> Handle(CountItems request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref counters.CountItems);
        return Task.FromResult(3);
    }
}

public record Touch : IRequest;

internal sealed class TouchHandler(Counters counters) : IRequestHandler<Touch>
{
    public Task Handle(Touch request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref counters.Touch);
        return Task.CompletedTask;
    }
}

public record GetQuote : IRequest<string>;

internal sealed class GetQuoteHandler(Counters counters) : IRequestHandler<GetQuote, string>
{
    public Task<string> Handle(GetQuote request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref counters.GetQuote);
        throw new TimeoutException("The quote service did not answer.");
    }
}

internal sealed class GetQuoteRetry : IRetryableRequest<GetQuote, string>
{
    public int RetryAttempts => 2;

    public int RetryDelay => 5;

    public int ExceptionsAllowedBeforeCircuitTrip => 100;
}

internal sealed class GetQuoteFallback : IFallbackHandler<GetQuote, string>
{
    public Task<string> HandleFallback(GetQuote request, CancellationToken cancellationToken) => Task.FromResult("fallback");
}

public sealed class ItemFilter : QueryBase
{
    public string? Category { get; set; }
}

/// <summary>The paged result: the handler echoes the paging, sorting and filter it received.</summary>
public sealed class ItemPage : IPagedResult<string>
{
    public ICollection<string> Results { get; set; } = [];

    public int Count { get; set; }
}

public sealed class FindItems : PagedAndSortedRequest<ItemPage, ItemFilter, string>;

internal sealed class FindItemsHandler : IRequestHandler<FindItems, ItemPage>
{
    public Task<ItemPage> Handle(FindItems request, CancellationToken cancellationToken)
        => Task.FromResult(new ItemPage
        {
            Count = 42,
            Results = [$"page {request.Page}", $"count {request.Count}", $"sorted by name: {request.IsSortBy("name")} {request.SortDir}", $"category {request.Filter?.Category}"],
        });
}

// The request, for a cache key built from generated metadata.
[JsonSerializable(typeof(CountItems))]
internal sealed partial class MediatorJsonContext : JsonSerializerContext;
