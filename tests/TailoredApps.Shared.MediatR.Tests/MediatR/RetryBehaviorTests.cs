using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests;

public class RetryBehaviorTests
{
    // RetryBehavior shares its circuit breaker per closed request/response type,
    // so every test gets its own request type to keep breaker state isolated.
    private class NoHandlerRequest : IRequest<string> { }

    private class TransientFailureRequest : IRequest<string> { }

    private class BackoffRequest : IRequest<string> { }

    private class ExhaustedRequest : IRequest<string> { }

    private class DefaultConfigRequest : IRequest<string> { }

    private class OpenCircuitRequest : IRequest<string> { }

    private class CancelledRequest : IRequest<string> { }

    private class CancelledWhileWaitingRequest : IRequest<string> { }

    private class RetryAfterRequest : IRequest<string> { }

    private class TrialSucceedsRequest : IRequest<string> { }

    private class TrialFailsRequest : IRequest<string> { }

    /// <summary>A clock the test moves by hand, so the five-second circuit break needs no waiting.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    private class RetryConfig<TRequest> : IRetryableRequest<TRequest, string> where TRequest : IRequest<string>
    {
        public int RetryAttempts { get; set; } = 2;

        public int RetryDelay { get; set; } = 1;

        public bool RetryWithExponentialBackoff { get; set; }

        public int ExceptionsAllowedBeforeCircuitTrip { get; set; } = 10;
    }

    /// <summary>Relies entirely on the IRetryableRequest default interface members.</summary>
    private class DefaultConfig<TRequest> : IRetryableRequest<TRequest, string> where TRequest : IRequest<string>
    {
    }

    private static RetryBehavior<TRequest, string> CreateSut<TRequest>(params IRetryableRequest<TRequest, string>[] retryHandlers)
        where TRequest : IRequest<string>
        => new RetryBehavior<TRequest, string>(retryHandlers, NullLogger<RetryBehavior<TRequest, string>>.Instance);

    private static RetryBehavior<TRequest, string> CreateSut<TRequest>(TimeProvider timeProvider, IRetryableRequest<TRequest, string> retryHandler)
        where TRequest : IRequest<string>
        => new RetryBehavior<TRequest, string>(new[] { retryHandler }, NullLogger<RetryBehavior<TRequest, string>>.Instance, timeProvider);

    private static readonly RequestHandlerDelegate<string> Failing = (ct) => throw new InvalidOperationException("persistent failure");

    private static readonly RequestHandlerDelegate<string> Succeeding = (ct) => Task.FromResult("success");

    [Fact]
    public async Task When_No_Retry_Handler_Is_Registered_Should_Invoke_Next_Exactly_Once()
    {
        // arrange
        var sut = CreateSut<NoHandlerRequest>();
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            return Task.FromResult("handler-response");
        };

        // act
        var response = await sut.Handle(new NoHandlerRequest(), next, CancellationToken.None);

        // assert
        Assert.Equal("handler-response", response);
        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task When_Next_Fails_Twice_With_Two_Retry_Attempts_Should_Succeed_After_Three_Invocations()
    {
        // arrange
        var sut = CreateSut(new RetryConfig<TransientFailureRequest>());
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            if (nextCalls <= 2)
            {
                throw new InvalidOperationException("transient failure");
            }

            return Task.FromResult("success");
        };

        // act
        var response = await sut.Handle(new TransientFailureRequest(), next, CancellationToken.None);

        // assert
        Assert.Equal("success", response);
        Assert.Equal(3, nextCalls);
    }

    [Fact]
    public async Task When_Next_Fails_Twice_With_Exponential_Backoff_Should_Succeed_After_Three_Invocations()
    {
        // arrange
        var sut = CreateSut(new RetryConfig<BackoffRequest> { RetryWithExponentialBackoff = true });
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            if (nextCalls <= 2)
            {
                throw new InvalidOperationException("transient failure");
            }

            return Task.FromResult("success");
        };

        // act
        var response = await sut.Handle(new BackoffRequest(), next, CancellationToken.None);

        // assert
        Assert.Equal("success", response);
        Assert.Equal(3, nextCalls);
    }

    [Fact]
    public async Task When_Next_Keeps_Failing_Should_Exhaust_Retries_And_Rethrow_Last_Exception()
    {
        // arrange
        var sut = CreateSut(new RetryConfig<ExhaustedRequest>());
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            throw new InvalidOperationException($"failure {nextCalls}");
        };

        // act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(new ExhaustedRequest(), next, CancellationToken.None));

        // assert - initial attempt + 2 retries = 3 invocations, last exception is rethrown
        Assert.Equal(3, nextCalls);
        Assert.Equal("failure 3", exception.Message);
    }

    [Fact]
    public async Task When_Using_Default_Retry_Config_Should_Retry_Once()
    {
        // arrange - DefaultConfig relies on the IRetryableRequest default interface members (RetryAttempts = 1)
        var sut = CreateSut(new DefaultConfig<DefaultConfigRequest>());
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            if (nextCalls == 1)
            {
                throw new InvalidOperationException("transient failure");
            }

            return Task.FromResult("success");
        };

        // act
        var response = await sut.Handle(new DefaultConfigRequest(), next, CancellationToken.None);

        // assert
        Assert.Equal("success", response);
        Assert.Equal(2, nextCalls);
    }

    [Fact]
    public async Task When_Requests_Keep_Failing_Should_Open_Circuit_And_Fail_Fast_Without_Invoking_Next()
    {
        // arrange - retries exhaust within the first request, tripping the breaker for the next one
        var config = new RetryConfig<OpenCircuitRequest> { RetryAttempts = 1, ExceptionsAllowedBeforeCircuitTrip = 1 };
        var sut = CreateSut(config);
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            throw new InvalidOperationException("persistent failure");
        };

        // act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Handle(new OpenCircuitRequest(), next, CancellationToken.None));
        var callsAfterFirstRequest = nextCalls;
        var secondException = await Assert.ThrowsAsync<CircuitBreakerOpenException>(
            () => sut.Handle(new OpenCircuitRequest(), next, CancellationToken.None));

        // assert - first request ran its attempts, the second failed fast on the open circuit
        Assert.Equal(2, callsAfterFirstRequest);
        Assert.Equal(2, nextCalls);
        Assert.NotNull(secondException);
    }

    [Fact]
    public async Task When_Request_Is_Cancelled_Should_Not_Retry_And_Should_Not_Open_The_Circuit()
    {
        // arrange - one failure would be enough to open this circuit
        var config = new RetryConfig<CancelledRequest> { RetryAttempts = 3, ExceptionsAllowedBeforeCircuitTrip = 1 };
        var sut = CreateSut(config);
        var nextCalls = 0;
        RequestHandlerDelegate<string> cancelled = (ct) =>
        {
            nextCalls++;
            throw new OperationCanceledException("the caller gave up");
        };

        // act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.Handle(new CancelledRequest(), cancelled, CancellationToken.None));
        var response = await sut.Handle(new CancelledRequest(), (ct) => Task.FromResult("success"), CancellationToken.None);

        // assert - the cancelled request ran once, and the next request still reached the handler
        Assert.Equal(1, nextCalls);
        Assert.Equal("success", response);
    }

    [Fact]
    public async Task When_Token_Is_Cancelled_While_Waiting_To_Retry_Should_Stop_Without_Another_Attempt()
    {
        // arrange - the wait between attempts is far longer than the test is willing to run
        var config = new RetryConfig<CancelledWhileWaitingRequest> { RetryAttempts = 5, RetryDelay = 30_000 };
        var sut = CreateSut(config);
        using var cancellation = new CancellationTokenSource();
        var nextCalls = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            nextCalls++;
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
            throw new InvalidOperationException("transient failure");
        };

        // act
        var started = DateTime.UtcNow;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.Handle(new CancelledWhileWaitingRequest(), next, cancellation.Token));

        // assert
        Assert.Equal(1, nextCalls);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10), "the retry wait was not cancelled");
    }

    [Fact]
    public async Task When_Circuit_Is_Open_Should_Say_Which_Request_And_For_How_Long()
    {
        // arrange
        var clock = new ManualTimeProvider();
        var sut = CreateSut(clock, new RetryConfig<RetryAfterRequest> { RetryAttempts = 0, ExceptionsAllowedBeforeCircuitTrip = 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(new RetryAfterRequest(), Failing, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(2));

        // act
        var exception = await Assert.ThrowsAsync<CircuitBreakerOpenException>(
            () => sut.Handle(new RetryAfterRequest(), Succeeding, CancellationToken.None));

        // assert - five seconds of break, two already gone
        Assert.Equal(typeof(RetryAfterRequest), exception.RequestType);
        Assert.Equal(TimeSpan.FromSeconds(3), exception.RetryAfter);
    }

    [Fact]
    public async Task When_Break_Has_Passed_And_The_Trial_Request_Succeeds_Should_Close_The_Circuit()
    {
        // arrange - open the circuit, then let the break pass
        var clock = new ManualTimeProvider();
        var sut = CreateSut(clock, new RetryConfig<TrialSucceedsRequest> { RetryAttempts = 0, ExceptionsAllowedBeforeCircuitTrip = 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(new TrialSucceedsRequest(), Failing, CancellationToken.None));
        await Assert.ThrowsAsync<CircuitBreakerOpenException>(() => sut.Handle(new TrialSucceedsRequest(), Succeeding, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(6));

        // act
        var trial = await sut.Handle(new TrialSucceedsRequest(), Succeeding, CancellationToken.None);
        var afterwards = await sut.Handle(new TrialSucceedsRequest(), Succeeding, CancellationToken.None);

        // assert
        Assert.Equal("success", trial);
        Assert.Equal("success", afterwards);
    }

    [Fact]
    public async Task When_Break_Has_Passed_And_The_Trial_Request_Fails_Should_Open_The_Circuit_Again()
    {
        // arrange - three failures in a row are allowed before the circuit opens for the first time
        var clock = new ManualTimeProvider();
        var sut = CreateSut(clock, new RetryConfig<TrialFailsRequest> { RetryAttempts = 0, ExceptionsAllowedBeforeCircuitTrip = 3 });
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(new TrialFailsRequest(), Failing, CancellationToken.None));
        }

        clock.Advance(TimeSpan.FromSeconds(6));

        // act - a single failed trial is enough to open it again
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(new TrialFailsRequest(), Failing, CancellationToken.None));
        var exception = await Assert.ThrowsAsync<CircuitBreakerOpenException>(
            () => sut.Handle(new TrialFailsRequest(), Succeeding, CancellationToken.None));

        // assert
        Assert.Equal(TimeSpan.FromSeconds(5), exception.RetryAfter);
    }
}
