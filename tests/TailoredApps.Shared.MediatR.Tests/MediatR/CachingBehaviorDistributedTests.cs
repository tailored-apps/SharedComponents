using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests;

/// <summary>
/// <see cref="CachingBehavior{TRequest, TResponse}"/> with a distributed cache behind HybridCache. Each
/// "application instance" below has its own HybridCache (its own in-process memory) but they share one
/// <see cref="IDistributedCache"/>, as instances of a service share Redis: what one instance stores has to
/// reach the other only through bytes written to that store.
/// </summary>
public class CachingBehaviorDistributedTests
{
    /// <summary>An in-process distributed cache that records how it is used and can be switched off.</summary>
    private sealed class SharedStore : IDistributedCache
    {
        private readonly MemoryDistributedCache inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public ConcurrentDictionary<string, DistributedCacheEntryOptions> Written { get; } = new();

        public ConcurrentQueue<string> Reads { get; } = new();

        public bool Unavailable { get; set; }

        private void ThrowIfUnavailable()
        {
            if (Unavailable)
                throw new InvalidOperationException("The distributed cache cannot be reached.");
        }

        public byte[] Get(string key)
        {
            ThrowIfUnavailable();
            Reads.Enqueue(key);
            return inner.Get(key);
        }

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            ThrowIfUnavailable();
            Reads.Enqueue(key);
            return inner.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            ThrowIfUnavailable();
            Written[key] = options;
            inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            ThrowIfUnavailable();
            Written[key] = options;
            return inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);

        public void Remove(string key)
        {
            ThrowIfUnavailable();
            inner.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            ThrowIfUnavailable();
            return inner.RemoveAsync(key, token);
        }
    }

    private sealed class Policy<TRequest, TResponse> : ICachePolicy<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        public Func<TRequest, string> Key { get; set; } = _ => "key";

        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; } = TimeSpan.FromMinutes(5);

        public string GetCacheKey(TRequest request) => Key(request);
    }

    public class ProductRequest : IRequest<Product>
    {
        public int Id { get; set; }
    }

    public class CountRequest : IRequest<int>
    {
        public int Id { get; set; }
    }

    public enum Availability
    {
        Unknown,
        InStock,
        SoldOut,
    }

    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public decimal Price { get; set; }

        public Availability Availability { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public string Description { get; set; }

        public List<string> Tags { get; set; }

        public Dictionary<string, decimal> PricesByCurrency { get; set; }
    }

    /// <summary>One application instance: its own HybridCache on top of the shared store.</summary>
    private sealed class Instance<TRequest, TResponse> : IDisposable where TRequest : IRequest<TResponse>
    {
        private readonly ServiceProvider services;
        private readonly CachingBehavior<TRequest, TResponse> behavior;

        public Instance(SharedStore store, Policy<TRequest, TResponse> policy)
        {
            var collection = new ServiceCollection();
            collection.AddSingleton<IDistributedCache>(store);
            collection.AddHybridCache();
            services = collection.BuildServiceProvider();
            Cache = services.GetRequiredService<HybridCache>();
            behavior = new CachingBehavior<TRequest, TResponse>(
                Cache, NullLogger<CachingBehavior<TRequest, TResponse>>.Instance, new[] { policy }, null);
        }

        public HybridCache Cache { get; }

        public int HandlerCalls { get; private set; }

        public Task<TResponse> Send(TRequest request, Func<TResponse> handler)
            => behavior.Handle(
                request,
                _ =>
                {
                    HandlerCalls++;
                    return Task.FromResult(handler());
                },
                CancellationToken.None);

        public void Dispose() => services.Dispose();
    }

    private static Product Tea() => new()
    {
        Id = 7,
        Name = "Herbata „Zielona”",
        Price = 12.99m,
        Availability = Availability.InStock,
        UpdatedAt = new DateTimeOffset(2026, 11, 12, 10, 30, 0, TimeSpan.FromHours(1)),
        Description = null,
        Tags = ["tea", "green"],
        PricesByCurrency = new Dictionary<string, decimal> { ["PLN"] = 12.99m, ["EUR"] = 2.99m },
    };

    [Fact]
    public async Task When_One_Instance_Cached_A_Response_Should_Serve_It_On_Another_Instance_Without_Calling_The_Handler()
    {
        // arrange
        var store = new SharedStore();
        var policy = new Policy<ProductRequest, Product> { Key = r => "product:" + r.Id };
        using var first = new Instance<ProductRequest, Product>(store, policy);
        using var second = new Instance<ProductRequest, Product>(store, policy);

        // act
        await first.Send(new ProductRequest { Id = 7 }, Tea);
        var served = await second.Send(new ProductRequest { Id = 7 }, () => throw new InvalidOperationException("the handler must not run"));

        // assert
        Assert.Equal(1, first.HandlerCalls);
        Assert.Equal(0, second.HandlerCalls);
        Assert.Equal("Herbata „Zielona”", served.Name);
        Assert.Contains("product:7", store.Written.Keys);
        Assert.Contains("product:7", store.Reads);
    }

    [Fact]
    public async Task When_A_Response_Travels_Through_The_Distributed_Cache_Should_Arrive_With_Every_Value_Intact()
    {
        // arrange
        var store = new SharedStore();
        var policy = new Policy<ProductRequest, Product>();
        using var first = new Instance<ProductRequest, Product>(store, policy);
        using var second = new Instance<ProductRequest, Product>(store, policy);
        var original = Tea();

        // act
        await first.Send(new ProductRequest { Id = 7 }, () => original);
        var copy = await second.Send(new ProductRequest { Id = 7 }, () => null);

        // assert: a different object, rebuilt from the stored bytes
        Assert.NotSame(original, copy);
        Assert.Equal(original.Id, copy.Id);
        Assert.Equal(original.Name, copy.Name);
        Assert.Equal(original.Price, copy.Price);
        Assert.Equal(Availability.InStock, copy.Availability);
        Assert.Equal(original.UpdatedAt, copy.UpdatedAt);
        Assert.Equal(TimeSpan.FromHours(1), copy.UpdatedAt.Offset);
        Assert.Null(copy.Description);
        Assert.Equal(original.Tags, copy.Tags);
        Assert.Equal(original.PricesByCurrency, copy.PricesByCurrency);
    }

    [Fact]
    public async Task When_The_Cached_Response_Is_A_Default_Value_Should_Still_Be_A_Hit_On_Another_Instance()
    {
        // arrange: zero is a real answer, not "nothing cached"
        var store = new SharedStore();
        var policy = new Policy<CountRequest, int>();
        using var first = new Instance<CountRequest, int>(store, policy);
        using var second = new Instance<CountRequest, int>(store, policy);

        // act
        await first.Send(new CountRequest { Id = 1 }, () => 0);
        var served = await second.Send(new CountRequest { Id = 1 }, () => 99);

        // assert
        Assert.Equal(0, served);
        Assert.Equal(0, second.HandlerCalls);
    }

    [Fact]
    public async Task When_Requests_Have_Different_Keys_Should_Not_Share_An_Entry()
    {
        // arrange
        var store = new SharedStore();
        var policy = new Policy<ProductRequest, Product> { Key = r => "product:" + r.Id };
        using var first = new Instance<ProductRequest, Product>(store, policy);
        using var second = new Instance<ProductRequest, Product>(store, policy);

        // act
        await first.Send(new ProductRequest { Id = 7 }, Tea);
        var other = await second.Send(new ProductRequest { Id = 8 }, () => new Product { Id = 8, Name = "Coffee" });

        // assert
        Assert.Equal("Coffee", other.Name);
        Assert.Equal(1, second.HandlerCalls);
        Assert.Equal(new[] { "product:7", "product:8" }, store.Written.Keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public async Task When_An_Entry_Is_Stored_Should_Give_The_Distributed_Cache_The_Expiration_Of_The_Policy()
    {
        // arrange
        var store = new SharedStore();
        var policy = new Policy<ProductRequest, Product> { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20) };
        using var instance = new Instance<ProductRequest, Product>(store, policy);

        // act
        await instance.Send(new ProductRequest { Id = 7 }, Tea);

        // assert
        var options = Assert.Single(store.Written).Value;
        Assert.Equal(TimeSpan.FromMinutes(20), options.AbsoluteExpirationRelativeToNow);
        Assert.Null(options.SlidingExpiration);
    }

    [Fact]
    public async Task When_An_Entry_Is_Removed_On_One_Instance_Should_Be_Gone_For_An_Instance_That_Had_Not_Read_It()
    {
        // arrange
        var store = new SharedStore();
        var policy = new Policy<ProductRequest, Product> { Key = r => "product:" + r.Id };
        using var first = new Instance<ProductRequest, Product>(store, policy);
        using var second = new Instance<ProductRequest, Product>(store, policy);
        await first.Send(new ProductRequest { Id = 7 }, Tea);

        // act
        await first.Cache.RemoveAsync("product:7");
        var fresh = await second.Send(new ProductRequest { Id = 7 }, () => new Product { Id = 7, Name = "Updated" });

        // assert
        Assert.Equal("Updated", fresh.Name);
        Assert.Equal(1, second.HandlerCalls);
    }

    [Fact]
    public async Task When_The_Distributed_Cache_Cannot_Be_Reached_Should_Answer_From_The_Handler()
    {
        // arrange: an outage of the cache must not become an outage of the request
        var store = new SharedStore { Unavailable = true };
        using var instance = new Instance<ProductRequest, Product>(store, new Policy<ProductRequest, Product>());

        // act
        var response = await instance.Send(new ProductRequest { Id = 7 }, Tea);

        // assert
        Assert.Equal("Herbata „Zielona”", response.Name);
        Assert.Equal(1, instance.HandlerCalls);
        Assert.Empty(store.Written);
    }
}
