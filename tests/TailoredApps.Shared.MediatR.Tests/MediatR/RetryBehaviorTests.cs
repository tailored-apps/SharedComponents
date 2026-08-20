using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using TailoredApps.Shared.MediatR.Interfaces.Messages;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
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
            var secondException = await Assert.ThrowsAnyAsync<BrokenCircuitException>(
                () => sut.Handle(new OpenCircuitRequest(), next, CancellationToken.None));

            // assert - first request ran its attempts, the second failed fast on the open circuit
            Assert.Equal(2, callsAfterFirstRequest);
            Assert.Equal(2, nextCalls);
            Assert.NotNull(secondException);
        }
    }
}
