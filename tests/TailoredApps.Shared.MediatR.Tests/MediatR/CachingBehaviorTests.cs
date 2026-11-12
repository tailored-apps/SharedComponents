using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ForkForward.Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TailoredApps.Shared.MediatR.Interfaces.Caching;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests;

public class CachingBehaviorTests
{
    /// <summary>A real HybridCache (in-process only), as RegisterPipelineBehaviors sets it up.</summary>
    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static CachingBehavior<TRequest, TResponse> CreateSut<TRequest, TResponse>(
        HybridCache cache, ICachePolicy<TRequest, TResponse> policy, TimeProvider timeProvider = null)
        where TRequest : IRequest<TResponse>
        => new CachingBehavior<TRequest, TResponse>(
            cache,
            NullLogger<CachingBehavior<TRequest, TResponse>>.Instance,
            policy == null ? Array.Empty<ICachePolicy<TRequest, TResponse>>() : new[] { policy },
            timeProvider);

    private sealed class Policy<TRequest, TResponse> : ICachePolicy<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        public Func<TRequest, string> Key { get; set; } = _ => "key";

        public DateTime? AbsoluteExpiration { get; set; }

        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; } = TimeSpan.FromMinutes(5);

        public TimeSpan? LocalCacheExpiration { get; set; }

        public IReadOnlyCollection<string> Tags { get; set; }

        public string GetCacheKey(TRequest request) => Key(request);
    }

    /// <summary>Runs the factory every time and remembers what the behaviour asked the cache for.</summary>
    private sealed class RecordingHybridCache : HybridCache
    {
        public string Key { get; private set; }

        public HybridCacheEntryOptions Options { get; private set; }

        public IReadOnlyCollection<string> Tags { get; private set; }

        public int Calls { get; private set; }

        public override async ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions options = null,
            IEnumerable<string> tags = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Key = key;
            Options = options;
            Tags = tags?.ToList();
            return await factory(state, cancellationToken);
        }

        public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions options = null, IEnumerable<string> tags = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // ── caching against a real HybridCache ───────────────────────────────────────────────

    [Fact]
    public async Task When_No_Cache_Policy_Is_Registered_Should_Invoke_Next_Once_And_Not_Touch_Cache()
    {
        // arrange: a strict mock fails the test on any cache access
        var cache = new Mock<HybridCache>(MockBehavior.Strict);
        var sut = CreateSut<CachedRequest, string>(cache.Object, policy: null);
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            return Task.FromResult("handler-response");
        };

        // act
        var response = await sut.Handle(new CachedRequest { Id = 1 }, next, CancellationToken.None);

        // assert
        Assert.Equal("handler-response", response);
        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task When_The_Same_Request_Is_Sent_Twice_Should_Invoke_Next_Once_And_Return_The_Cached_Response()
    {
        // arrange
        var sut = CreateSut(CreateCache(), new Policy<CachedRequest, string>());
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("response-" + ++nextCalls);

        // act
        var first = await sut.Handle(new CachedRequest { Id = 7 }, next, CancellationToken.None);
        var second = await sut.Handle(new CachedRequest { Id = 7 }, next, CancellationToken.None);

        // assert
        Assert.Equal("response-1", first);
        Assert.Equal("response-1", second);
        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task When_Requests_Have_Different_Cache_Keys_Should_Invoke_Next_For_Each()
    {
        // arrange
        var policy = new Policy<CachedRequest, string> { Key = request => "request-" + request.Id };
        var sut = CreateSut(CreateCache(), policy);
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("response-" + ++nextCalls);

        // act
        var first = await sut.Handle(new CachedRequest { Id = 1 }, next, CancellationToken.None);
        var second = await sut.Handle(new CachedRequest { Id = 2 }, next, CancellationToken.None);

        // assert
        Assert.Equal("response-1", first);
        Assert.Equal("response-2", second);
        Assert.Equal(2, nextCalls);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(0)] // a default value is a response like any other
    public async Task When_Response_Is_A_Value_Type_Should_Cache_It(int value)
    {
        // arrange
        var sut = CreateSut(CreateCache(), new Policy<CachedCountRequest, int>());
        var nextCalls = 0;
        RequestHandlerDelegate<int> next = (ct) =>
        {
            nextCalls++;
            return Task.FromResult(value);
        };

        // act
        var first = await sut.Handle(new CachedCountRequest { Id = 5 }, next, CancellationToken.None);
        var second = await sut.Handle(new CachedCountRequest { Id = 5 }, next, CancellationToken.None);

        // assert
        Assert.Equal(value, first);
        Assert.Equal(value, second);
        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task When_Response_Is_Null_Should_Cache_It()
    {
        // arrange
        var sut = CreateSut(CreateCache(), new Policy<CachedRequest, string>());
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            return Task.FromResult<string>(null);
        };

        // act
        var first = await sut.Handle(new CachedRequest { Id = 3 }, next, CancellationToken.None);
        var second = await sut.Handle(new CachedRequest { Id = 3 }, next, CancellationToken.None);

        // assert
        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task When_Next_Throws_Should_Cache_Nothing_And_Invoke_Next_Again()
    {
        // arrange
        var sut = CreateSut(CreateCache(), new Policy<CachedRequest, string>());
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            if (nextCalls == 1)
            {
                throw new InvalidOperationException("handler failed");
            }

            return Task.FromResult("recovered");
        };

        // act
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(new CachedRequest { Id = 9 }, next, CancellationToken.None));
        var response = await sut.Handle(new CachedRequest { Id = 9 }, next, CancellationToken.None);

        // assert
        Assert.Equal("recovered", response);
        Assert.Equal(2, nextCalls);
    }

    [Fact]
    public async Task When_Many_Identical_Requests_Arrive_At_Once_Should_Invoke_Next_Once()
    {
        // arrange: the handler stays busy until every request has been sent
        var sut = CreateSut(CreateCache(), new Policy<CachedRequest, string>());
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = async (ct) =>
        {
            Interlocked.Increment(ref nextCalls);
            await release.Task;
            return "shared-response";
        };

        // act
        var requests = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => sut.Handle(new CachedRequest { Id = 4 }, next, CancellationToken.None)))
            .ToArray();
        await Task.Delay(100);
        release.SetResult(true);
        var responses = await Task.WhenAll(requests);

        // assert
        Assert.All(responses, response => Assert.Equal("shared-response", response));
        Assert.Equal(1, nextCalls);
    }

    // ── what the policy asks the cache for ───────────────────────────────────────────────

    [Fact]
    public async Task When_Policy_Sets_A_Relative_Expiration_Should_Pass_It_With_Key_Tags_And_Local_Expiration()
    {
        // arrange
        var cache = new RecordingHybridCache();
        var policy = new Policy<CachedRequest, string>
        {
            Key = request => "cache-key-" + request.Id,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(7),
            LocalCacheExpiration = TimeSpan.FromSeconds(20),
            Tags = new[] { "orders", "tenant-1" },
        };
        var sut = CreateSut(cache, policy);

        // act
        var response = await sut.Handle(new CachedRequest { Id = 3 }, (ct) => Task.FromResult("fresh"), CancellationToken.None);

        // assert
        Assert.Equal("fresh", response);
        Assert.Equal("cache-key-3", cache.Key);
        Assert.Equal(TimeSpan.FromMinutes(7), cache.Options.Expiration);
        Assert.Equal(TimeSpan.FromSeconds(20), cache.Options.LocalCacheExpiration);
        Assert.Equal(new[] { "orders", "tenant-1" }, cache.Tags);
    }

    [Fact]
    public async Task When_Policy_Sets_Both_Expirations_Should_Prefer_The_Relative_One()
    {
        // arrange
        var cache = new RecordingHybridCache();
        var policy = new Policy<CachedRequest, string>
        {
            AbsoluteExpiration = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(90),
        };
        var sut = CreateSut(cache, policy);

        // act
        await sut.Handle(new CachedRequest { Id = 1 }, (ct) => Task.FromResult("fresh"), CancellationToken.None);

        // assert
        Assert.Equal(TimeSpan.FromSeconds(90), cache.Options.Expiration);
    }

    [Fact]
    public async Task When_Policy_Sets_Only_An_Absolute_Expiration_Should_Cache_Until_That_Moment()
    {
        // arrange
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var cache = new RecordingHybridCache();
        var policy = new Policy<CachedRequest, string>
        {
            AbsoluteExpiration = now.UtcDateTime.AddMinutes(30),
            AbsoluteExpirationRelativeToNow = null,
        };
        var sut = CreateSut(cache, policy, new FixedTimeProvider(now));

        // act
        await sut.Handle(new CachedRequest { Id = 1 }, (ct) => Task.FromResult("fresh"), CancellationToken.None);

        // assert
        Assert.Equal(TimeSpan.FromMinutes(30), cache.Options.Expiration);
    }

    [Fact]
    public async Task When_Absolute_Expiration_Has_Passed_Should_Invoke_Next_Without_Caching()
    {
        // arrange
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var cache = new RecordingHybridCache();
        var policy = new Policy<CachedRequest, string>
        {
            AbsoluteExpiration = now.UtcDateTime.AddSeconds(-1),
            AbsoluteExpirationRelativeToNow = null,
        };
        var sut = CreateSut(cache, policy, new FixedTimeProvider(now));

        // act
        var response = await sut.Handle(new CachedRequest { Id = 1 }, (ct) => Task.FromResult("fresh"), CancellationToken.None);

        // assert
        Assert.Equal("fresh", response);
        Assert.Equal(0, cache.Calls);
    }

    [Fact]
    public async Task When_Policy_Sets_No_Expiration_Should_Leave_The_Lifetime_To_The_Cache_Defaults()
    {
        // arrange
        var cache = new RecordingHybridCache();
        var policy = new Policy<CachedRequest, string> { AbsoluteExpirationRelativeToNow = null };
        var sut = CreateSut(cache, policy);

        // act
        await sut.Handle(new CachedRequest { Id = 1 }, (ct) => Task.FromResult("fresh"), CancellationToken.None);

        // assert
        Assert.Equal(1, cache.Calls);
        Assert.Null(cache.Options);
    }

    public class CachedCountRequest : IRequest<int>
    {
        public int Id { get; set; }
    }
}
