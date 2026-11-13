using System.Text.Json.Serialization;
using ForkForward.Mediator;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.AotSmoke.Caching;
using TailoredApps.Shared.MediatR.Caching;
using TailoredApps.Shared.MediatR.DI;
using TailoredApps.Shared.MediatR.Interfaces.Caching;

// The response cache of the MediatR libraries, natively: a cached object and a cached value, serialized with
// JSON metadata generated at compile time. See the project file for why this is an application of its own.

var failures = 0;

void Check(string name, bool passed, string? detail = null)
{
    Console.WriteLine($"{(passed ? "ok  " : "FAIL")} {name}{(passed || detail is null ? string.Empty : $" - {detail}")}");
    if (!passed)
        failures++;
}

var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<Counters>();
services.AddMediator(cfg =>
{
    cfg.AddRequestHandler<GetPrice, PriceDto, GetPriceHandler>();
    cfg.AddRequestHandler<CountItems, int, CountItemsHandler>();
});

new PipelineRegistration(services)
    .AddPipelineBehaviors<GetPrice, PriceDto>()
    .AddPipelineBehaviors<CountItems, int>()
    .AddCachePolicy<GetPrice, PriceDto, GetPricePolicy>()
    .AddCachePolicy<CountItems, int, CountItemsPolicy>()
    .AddCacheSerialization(CacheJsonContext.Default);

await using var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
var counters = provider.GetRequiredService<Counters>();

try
{
    var first = await mediator.Send(new GetPrice(7));
    var second = await mediator.Send(new GetPrice(7));
    var other = await mediator.Send(new GetPrice(8));
    Check("cache: an object response is cached per key", first == new PriceDto(7, 129.00m) && second == first && other.ProductId == 8 && counters.GetPrice == 2,
        $"{first} {second} {other}, handler calls {counters.GetPrice}");
    Check("cache: the cached object is a copy, not the handler's instance", !ReferenceEquals(first, second));
}
catch (Exception exception)
{
    Check("cache: an object response is cached per key", false, $"{exception.GetType().Name}: {exception.Message}");
}

try
{
    var first = await mediator.Send(new CountItems("tea"));
    var second = await mediator.Send(new CountItems("tea"));
    Check("cache: a value-type response is cached", first == 3 && second == 3 && counters.CountItems == 1, $"{first} {second}, handler calls {counters.CountItems}");
}
catch (Exception exception)
{
    Check("cache: a value-type response is cached", false, $"{exception.GetType().Name}: {exception.Message}");
}

// The registration an existing application has: the five behaviors as open generic types. The container can
// close them natively when the response is a reference type.
var open = new ServiceCollection();
open.AddLogging();
open.AddSingleton<Counters>();
open.AddMediator(cfg =>
{
    cfg.AddRequestHandler<GetPrice, PriceDto, GetPriceHandler>();
    cfg.AddRequestHandler<CountItems, int, CountItemsHandler>();
});
var openPipeline = new PipelineRegistration(open);
openPipeline.RegisterPipelineBehaviors();
openPipeline
    .AddCachePolicy<GetPrice, PriceDto, GetPricePolicy>()
    .AddCacheSerialization(CacheJsonContext.Default);

await using var openProvider = open.BuildServiceProvider();
var openMediator = openProvider.GetRequiredService<IMediator>();
var openCounters = openProvider.GetRequiredService<Counters>();

try
{
    var first = await openMediator.Send(new GetPrice(7));
    var second = await openMediator.Send(new GetPrice(7));
    Check("cache: the open behaviors serve a reference-type response", first == new PriceDto(7, 129.00m) && second == first && openCounters.GetPrice == 1,
        $"{first} {second}, handler calls {openCounters.GetPrice}");
}
catch (Exception exception)
{
    Check("cache: the open behaviors serve a reference-type response", false, $"{exception.GetType().Name}: {exception.Message}");
}

// A value-type response is the case they cannot serve: there the container answers "Unable to create a generic
// service for type 'IPipelineBehavior`2[CountItems,Int32]' because 'System.Int32' is a ValueType", which is why
// the first container registers the behaviors closed. That is the platform's rule, so it is not a check here.

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures;

namespace TailoredApps.AotSmoke.Caching
{
    internal sealed class Counters
    {
        public int GetPrice;
        public int CountItems;
    }

    public record PriceDto(int ProductId, decimal Price);

    public record GetPrice(int ProductId) : ICachableRequest<PriceDto>
    {
        public string GetCacheKey() => $"price:{ProductId}";
    }

    internal sealed class GetPricePolicy : ICachePolicy<GetPrice, PriceDto>
    {
        public string GetCacheKey(GetPrice request) => request.GetCacheKey();
    }

    internal sealed class GetPriceHandler(Counters counters) : IRequestHandler<GetPrice, PriceDto>
    {
        public Task<PriceDto> Handle(GetPrice request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref counters.GetPrice);
            return Task.FromResult(new PriceDto(request.ProductId, 129.00m));
        }
    }

    public record CountItems(string Category) : IRequest<int>;

    internal sealed class CountItemsPolicy : ICachePolicy<CountItems, int>
    {
        public string GetCacheKey(CountItems request) => $"count:{request.Category}";
    }

    internal sealed class CountItemsHandler(Counters counters) : IRequestHandler<CountItems, int>
    {
        public Task<int> Handle(CountItems request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref counters.CountItems);
            return Task.FromResult(3);
        }
    }

    // The cached response types: HybridCache keeps a serialized copy of each.
    [JsonSerializable(typeof(PriceDto))]
    [JsonSerializable(typeof(int))]
    internal sealed partial class CacheJsonContext : JsonSerializerContext;
}
