using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.Providers;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests;

/// <summary>
/// ConfigureExceptionHandler in a real pipeline: the answer the provider chooses, and what ends up in the log.
/// </summary>
public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task When_Validation_Fails_Should_Answer_400_Without_An_Error_In_The_Log()
    {
        var log = new LogRecorder();
        using var host = await StartAsync(log, () => throw new ValidationException([new ValidationFailure("Id", "'Id' must be greater than '0'.")]));

        var response = await host.GetTestClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"field\": \"Id\"", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(log.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Debug && e.Category == "TailoredApps.Shared.ExceptionHandling");
    }

    [Fact]
    public async Task When_The_Provider_Answers_404_Should_Send_The_404_Instead_Of_Rethrowing()
    {
        var log = new LogRecorder();
        using var host = await StartAsync(log, () => throw new KeyNotFoundException("Order 7 does not exist."));

        var response = await host.GetTestClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Order 7 does not exist.", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(log.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task When_The_Exception_Is_Unexpected_Should_Answer_500_And_Log_The_Error()
    {
        var log = new LogRecorder();
        using var host = await StartAsync(log, () => throw new InvalidOperationException("The connection string is Server=db;Password=secret."));

        var response = await host.GetTestClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Password", await response.Content.ReadAsStringAsync());
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    private static async Task<IHost> StartAsync(LogRecorder log, Action fail)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment(Environments.Production)
                .ConfigureLogging(logging => logging.ClearProviders().SetMinimumLevel(LogLevel.Debug).AddProvider(log))
                .ConfigureServices(services => services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, NotFoundAwareProvider>())
                .Configure(app =>
                {
                    app.ConfigureExceptionHandler();
                    app.Run(_ =>
                    {
                        fail();
                        return Task.CompletedTask;
                    });
                }))
            .Build();
        await host.StartAsync();
        return host;
    }

    /// <summary>The defaults, plus the 404 mapping an application typically adds.</summary>
    private sealed class NotFoundAwareProvider : IExceptionHandlingProvider
    {
        private readonly DefaultExceptionHandlingProvider defaults = new();

        public ExceptionHandlingResultModel Response(Exception exception)
            => exception is KeyNotFoundException
                ? new ExceptionHandlingResultModel(404, exception.Message, [new ExceptionOrValidationError(string.Empty, exception.Message)])
                : defaults.Response(exception);

        public ExceptionHandlingResultModel Response(ModelStateDictionary modelState) => defaults.Response(modelState);
    }

    private sealed class LogRecorder : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Category, LogLevel Level, Exception Exception)> entries = new();

        public IReadOnlyCollection<(string Category, LogLevel Level, Exception Exception)> Entries => entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, entries);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<(string, LogLevel, Exception)> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => entries.Enqueue((category, logLevel, exception));
        }
    }
}
