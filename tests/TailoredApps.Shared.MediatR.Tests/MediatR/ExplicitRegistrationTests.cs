using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ForkForward.Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TailoredApps.Shared.MediatR.Caching;
using TailoredApps.Shared.MediatR.DI;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.Interfaces.DI;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests;

/// <summary>
/// Registration by name, without scanning: what a trimmed or NativeAOT application uses instead of
/// RegisterPipelineBehaviors(assembly) and the open generic behaviors.
/// </summary>
public class ExplicitRegistrationTests
{
    public record GetStock(int ProductId) : IRequest<StockDto>;

    public record StockDto(int ProductId, int Quantity);

    public record CountStock(string Warehouse) : IRequest<int>;

    public class Calls
    {
        public int GetStock;
        public int CountStock;
    }

    private sealed class GetStockHandler : IRequestHandler<GetStock, StockDto>
    {
        private readonly Calls calls;

        public GetStockHandler(Calls calls) => this.calls = calls;

        public Task<StockDto> Handle(GetStock request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls.GetStock);
            return Task.FromResult(new StockDto(request.ProductId, 12));
        }
    }

    private sealed class CountStockHandler : IRequestHandler<CountStock, int>
    {
        private readonly Calls calls;

        public CountStockHandler(Calls calls) => this.calls = calls;

        public Task<int> Handle(CountStock request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls.CountStock);
            throw new TimeoutException("the warehouse did not answer");
        }
    }

    private sealed class GetStockPolicy : ICachePolicy<GetStock, StockDto>
    {
        public string GetCacheKey(GetStock request) => $"stock:{request.ProductId}";
    }

    private sealed class CountStockRetry : IRetryableRequest<CountStock, int>
    {
        public int RetryAttempts => 1;

        public int RetryDelay => 1;

        public int ExceptionsAllowedBeforeCircuitTrip => 100;
    }

    private sealed class OtherRetry : IRetryableRequest<CountStock, int>
    {
        public int RetryAttempts => 9;
    }

    private sealed class CountStockFallback : IFallbackHandler<CountStock, int>
    {
        public Task<int> HandleFallback(CountStock request, CancellationToken cancellationToken) => Task.FromResult(-1);
    }

    private sealed class OtherFallback : IFallbackHandler<CountStock, int>
    {
        public Task<int> HandleFallback(CountStock request, CancellationToken cancellationToken) => Task.FromResult(-2);
    }

    [Fact]
    public void When_Behaviors_Are_Added_For_A_Request_Should_Register_The_Five_Closed_In_Pipeline_Order()
    {
        var services = new ServiceCollection();

        new PipelineRegistration(services).AddPipelineBehaviors<GetStock, StockDto>();

        var behaviors = services.Where(s => s.ServiceType == typeof(IPipelineBehavior<GetStock, StockDto>)).Select(s => s.ImplementationType).ToArray();
        Assert.Equal(
            [
                typeof(LoggingBehavior<GetStock, StockDto>),
                typeof(ValidationBehavior<GetStock, StockDto>),
                typeof(CachingBehavior<GetStock, StockDto>),
                typeof(FallbackBehavior<GetStock, StockDto>),
                typeof(RetryBehavior<GetStock, StockDto>),
            ],
            behaviors);
        Assert.Contains(services, s => s.ServiceType == typeof(HybridCache));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IPipelineBehavior<,>));   // nothing open generic
    }

    [Fact]
    public void When_Behaviors_Are_Added_Without_Caching_Should_Leave_HybridCache_Out()
    {
        var services = new ServiceCollection();

        new PipelineRegistration(services).AddPipelineBehaviorsWithoutCaching<CountStock, int>();

        var behaviors = services.Where(s => s.ServiceType == typeof(IPipelineBehavior<CountStock, int>)).Select(s => s.ImplementationType).ToArray();
        Assert.Equal(
            [
                typeof(LoggingBehavior<CountStock, int>),
                typeof(ValidationBehavior<CountStock, int>),
                typeof(FallbackBehavior<CountStock, int>),
                typeof(RetryBehavior<CountStock, int>),
            ],
            behaviors);
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(HybridCache));
    }

    [Fact]
    public void When_Policies_Are_Added_By_Name_Should_Register_Them_And_Keep_The_First_Fallback_And_Retry()
    {
        var services = new ServiceCollection();

        new PipelineRegistration(services)
            .AddCachePolicy<GetStock, StockDto, GetStockPolicy>()
            .AddFallbackHandler<CountStock, int, CountStockFallback>()
            .AddFallbackHandler<CountStock, int, OtherFallback>()
            .AddRetryPolicy<CountStock, int, CountStockRetry>()
            .AddRetryPolicy<CountStock, int, OtherRetry>();

        Assert.Equal(typeof(GetStockPolicy), Assert.Single(services, s => s.ServiceType == typeof(ICachePolicy<GetStock, StockDto>)).ImplementationType);
        Assert.Equal(typeof(CountStockFallback), Assert.Single(services, s => s.ServiceType == typeof(IFallbackHandler<CountStock, int>)).ImplementationType);
        Assert.Equal(typeof(CountStockRetry), Assert.Single(services, s => s.ServiceType == typeof(IRetryableRequest<CountStock, int>)).ImplementationType);
    }

    [Fact]
    public async Task When_Everything_Is_Registered_By_Name_Should_Cache_Retry_And_Fall_Back()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<Calls>();
        services.AddMediator(cfg =>
        {
            cfg.AddRequestHandler<GetStock, StockDto, GetStockHandler>();
            cfg.AddRequestHandler<CountStock, int, CountStockHandler>();
        });
        new PipelineRegistration(services)
            .AddPipelineBehaviors<GetStock, StockDto>()
            .AddPipelineBehaviorsWithoutCaching<CountStock, int>()
            .AddCachePolicy<GetStock, StockDto, GetStockPolicy>()
            .AddRetryPolicy<CountStock, int, CountStockRetry>()
            .AddFallbackHandler<CountStock, int, CountStockFallback>()
            .AddCacheSerialization(StockJsonContext.Default);
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var calls = provider.GetRequiredService<Calls>();

        var first = await mediator.Send(new GetStock(7));
        var second = await mediator.Send(new GetStock(7));
        var count = await mediator.Send(new CountStock("north"));

        Assert.Equal(new StockDto(7, 12), first);
        Assert.Equal(first, second);
        Assert.Equal(1, calls.GetStock);      // the second answer came from the cache
        Assert.Equal(-1, count);              // the fallback
        Assert.Equal(2, calls.CountStock);    // the first call and one retry
    }

    [Fact]
    public void When_A_Context_Knows_The_Type_Should_Serialize_Cached_Responses_With_Its_Metadata()
    {
        var factory = new JsonContextCacheSerializerFactory(StockJsonContext.Default);

        Assert.True(factory.TryCreateSerializer<StockDto>(out var serializer));
        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(new StockDto(7, 12), buffer);
        var copy = serializer.Deserialize(new ReadOnlySequence<byte>(buffer.WrittenMemory));

        Assert.Equal(new StockDto(7, 12), copy);
        Assert.Equal("{\"ProductId\":7,\"Quantity\":12}", System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void When_A_Context_Does_Not_Know_The_Type_Should_Leave_It_To_Other_Factories()
    {
        var factory = new JsonContextCacheSerializerFactory(StockJsonContext.Default);

        Assert.False(factory.TryCreateSerializer<Calls>(out var serializer));
        Assert.Null(serializer);
    }

    [Fact]
    public void When_Cache_Serialization_Is_Added_Should_Register_The_Factory_With_HybridCache()
    {
        var services = new ServiceCollection();

        new PipelineRegistration(services).AddCacheSerialization(StockJsonContext.Default);

        Assert.Contains(services, s => s.ServiceType == typeof(IHybridCacheSerializerFactory) && s.ImplementationInstance is JsonContextCacheSerializerFactory);
        Assert.Throws<ArgumentNullException>(() => new PipelineRegistration(services).AddCacheSerialization(null));
    }

    [Fact]
    public void When_A_Key_Is_Built_From_Generated_Metadata_Should_Depend_On_The_Type_And_The_Content()
    {
        var key = CacheKeyGenerator.Generate(new GetStock(7), StockJsonContext.Default.GetStock);
        var same = CacheKeyGenerator.Generate(new GetStock(7), StockJsonContext.Default.GetStock);
        var other = CacheKeyGenerator.Generate(new GetStock(8), StockJsonContext.Default.GetStock);

        Assert.StartsWith(typeof(GetStock).FullName + ":", key);
        Assert.Equal(key, same);
        Assert.NotEqual(key, other);
        Assert.Throws<ArgumentNullException>(() => CacheKeyGenerator.Generate(new GetStock(7), null));
    }

    [Fact]
    public void When_Scanning_Is_Inspected_Should_Be_Marked_As_Unsafe_For_Trimming()
    {
        // Scanning finds policies by reflection; a trimmed or NativeAOT application has to be told at compile time.
        foreach (var type in new[] { typeof(IPipelineRegistration), typeof(PipelineRegistration) })
        {
            var scan = type.GetMethod(nameof(IPipelineRegistration.RegisterPipelineBehaviors), [typeof(Assembly)]);

            Assert.NotNull(scan.GetCustomAttribute<RequiresUnreferencedCodeAttribute>());
            Assert.NotNull(scan.GetCustomAttribute<RequiresDynamicCodeAttribute>());
        }

        var registerOpen = typeof(PipelineRegistration).GetMethod(nameof(PipelineRegistration.RegisterPipelineBehaviors), Type.EmptyTypes);
        Assert.Null(registerOpen.GetCustomAttribute<RequiresUnreferencedCodeAttribute>());
    }
}

[JsonSerializable(typeof(ExplicitRegistrationTests.GetStock))]
[JsonSerializable(typeof(ExplicitRegistrationTests.StockDto))]
internal sealed partial class StockJsonContext : JsonSerializerContext;
