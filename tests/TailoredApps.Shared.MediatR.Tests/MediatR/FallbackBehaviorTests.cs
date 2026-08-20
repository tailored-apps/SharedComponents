using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TailoredApps.Shared.MediatR.Interfaces.Handlers;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class FallbackBehaviorTests
    {
        private static FallbackBehavior<FallbackRequest, string> CreateSut(params IFallbackHandler<FallbackRequest, string>[] fallbackHandlers)
            => new FallbackBehavior<FallbackRequest, string>(fallbackHandlers, NullLogger<FallbackBehavior<FallbackRequest, string>>.Instance);

        [Fact]
        public async Task When_No_Fallback_Handler_Is_Registered_Should_Invoke_Next_Once_And_Return_Its_Response()
        {
            // arrange
            var sut = CreateSut();
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var response = await sut.Handle(new FallbackRequest(), next, CancellationToken.None);

            // assert
            Assert.Equal("handler-response", response);
            Assert.Equal(1, nextCalls);
        }

        [Fact]
        public async Task When_Next_Throws_Should_Return_Fallback_Handler_Response()
        {
            // arrange
            var request = new FallbackRequest();
            var fallbackMock = new Mock<IFallbackHandler<FallbackRequest, string>>();
            fallbackMock
                .Setup(h => h.HandleFallback(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync("fallback-response");
            var sut = CreateSut(fallbackMock.Object);
            RequestHandlerDelegate<string> next = (ct) => throw new InvalidOperationException("primary handler failed");

            // act
            var response = await sut.Handle(request, next, CancellationToken.None);

            // assert
            Assert.Equal("fallback-response", response);
            fallbackMock.Verify(h => h.HandleFallback(request, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task When_Next_Succeeds_Should_Return_Its_Response_And_Never_Invoke_Fallback()
        {
            // arrange
            var request = new FallbackRequest();
            var fallbackMock = new Mock<IFallbackHandler<FallbackRequest, string>>(MockBehavior.Strict);
            var sut = CreateSut(fallbackMock.Object);
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var response = await sut.Handle(request, next, CancellationToken.None);

            // assert
            Assert.Equal("handler-response", response);
            Assert.Equal(1, nextCalls);
            fallbackMock.Verify(h => h.HandleFallback(It.IsAny<FallbackRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
