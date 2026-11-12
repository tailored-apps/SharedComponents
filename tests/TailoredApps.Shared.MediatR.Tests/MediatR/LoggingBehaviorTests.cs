using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using ForkForward.Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TailoredApps.Shared.MediatR.PipelineBehaviours;
using Xunit;

namespace TailoredApps.Shared.MediatR.Tests;

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

    [Fact]
    public async Task When_Validation_Refuses_The_Request_Should_Log_Information_With_The_Property_Names_Only()
    {
        // arrange
        var logger = new RecordingLogger<LoggingRequest>();
        var sut = new LoggingBehavior<LoggingRequest, string>(logger);
        var refused = new ValidationException(
        [
            new ValidationFailure("Payload", "'Payload' must not be 'card-4111111111111111'."),
            new ValidationFailure("Payload", "'Payload' is too long."),
            new ValidationFailure("Email", "'Email' is not an e-mail address."),
        ]);
        RequestHandlerDelegate<string> next = (ct) => throw refused;

        // act
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => sut.Handle(new LoggingRequest { Payload = "card-4111111111111111" }, next, CancellationToken.None));

        // assert - rethrown, logged once below Error, naming the properties but not quoting the values
        Assert.Same(refused, exception);
        var entry = Assert.Single(logger.Entries, e => e.Level > LogLevel.Debug);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("invalid: Payload, Email", entry.Message);
        Assert.DoesNotContain("4111111111111111", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task When_The_Caller_Cancels_Should_Log_Information_Not_Error()
    {
        // arrange
        var logger = new RecordingLogger<LoggingRequest>();
        var sut = new LoggingBehavior<LoggingRequest, string>(logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        RequestHandlerDelegate<string> next = (ct) => throw new OperationCanceledException(cancellation.Token);

        // act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.Handle(new LoggingRequest { Payload = "data" }, next, cancellation.Token));

        // assert
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("cancelled"));
    }

    [Fact]
    public async Task When_The_Handler_Times_Out_While_The_Caller_Waits_Should_Log_Error()
    {
        // arrange - an HttpClient timeout inside the handler throws TaskCanceledException, but nobody cancelled the request
        var logger = new RecordingLogger<LoggingRequest>();
        var sut = new LoggingBehavior<LoggingRequest, string>(logger);
        RequestHandlerDelegate<string> next = (ct) => throw new TaskCanceledException("The request timed out.");

        // act
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => sut.Handle(new LoggingRequest { Payload = "data" }, next, CancellationToken.None));

        // assert
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is TaskCanceledException);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception Exception)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
