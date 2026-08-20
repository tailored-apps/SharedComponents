using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests
{
    public class LoggingBehaviorTests
    {
        [Fact]
        public async Task When_Next_Succeeds_Should_Return_Its_Response()
        {
            // arrange
            var sut = new LoggingBehavior<LoggingRequest, string>(NullLogger<LoggingRequest>.Instance);
            var nextCalls = 0;
            RequestHandlerDelegate<string> next = (ct) =>
            {
                nextCalls++;
                return Task.FromResult("handler-response");
            };

            // act
            var response = await sut.Handle(new LoggingRequest { Payload = "data" }, next, CancellationToken.None);

            // assert
            Assert.Equal("handler-response", response);
            Assert.Equal(1, nextCalls);
        }

        [Fact]
        public async Task When_Next_Throws_Should_Log_Error_And_Rethrow_Same_Exception()
        {
            // arrange
            var loggerMock = new Mock<ILogger<LoggingRequest>>();
            var sut = new LoggingBehavior<LoggingRequest, string>(loggerMock.Object);
            var thrown = new InvalidOperationException("handler blew up");
            RequestHandlerDelegate<string> next = (ct) => throw thrown;

            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.Handle(new LoggingRequest { Payload = "data" }, next, CancellationToken.None));

            // assert - the original exception is rethrown and logged at Error level
            Assert.Same(thrown, exception);
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    thrown,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task When_Next_Succeeds_Should_Not_Log_Any_Error()
        {
            // arrange
            var loggerMock = new Mock<ILogger<LoggingRequest>>();
            var sut = new LoggingBehavior<LoggingRequest, string>(loggerMock.Object);
            RequestHandlerDelegate<string> next = (ct) => Task.FromResult("handler-response");

            // act
            var response = await sut.Handle(new LoggingRequest { Payload = "data" }, next, CancellationToken.None);

            // assert
            Assert.Equal("handler-response", response);
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }
    }
}
