using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.Providers;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests;

/// <summary>
/// ExceptionHandling.MinimalApi in a real pipeline: the endpoint filters answer with the same error model as
/// the middleware and the MVC filter.
/// </summary>
public class ExceptionHandlingMinimalApiTests
{
    public record NewProduct(string Name, decimal Price);

    private sealed class NewProductValidator : AbstractValidator<NewProduct>
    {
        public NewProductValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Price).GreaterThan(0);
        }
    }

    private sealed class NameIsNotReserved : AbstractValidator<NewProduct>
    {
        public NameIsNotReserved() => RuleFor(p => p.Name).NotEqual("reserved");
    }

    [Fact]
    public async Task When_The_Endpoint_Throws_A_Mapped_Exception_Should_Answer_With_The_Provider_Model()
    {
        var log = new LogRecorder();
        using var host = await StartAsync(log, endpoints => endpoints.MapGet("/orders/7", string () => throw new KeyNotFoundException("Order 7 does not exist.")).WithExceptionHandling());

        var response = await host.GetTestClient().GetAsync("/orders/7");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Order 7 does not exist.", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(log.Entries, e => e.Level >= LogLevel.Warning);   // a client error is not an error in the log
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Debug && e.Category == "TailoredApps.Shared.ExceptionHandling");
    }

    [Fact]
    public async Task When_The_Endpoint_Throws_An_Unexpected_Exception_Should_Answer_A_Generic_500_And_Log_It()
    {
        var log = new LogRecorder();
        using var host = await StartAsync(log, endpoints => endpoints.MapGet("/boom", string () => throw new InvalidOperationException("Server=db;Password=secret")).WithExceptionHandling());

        var response = await host.GetTestClient().GetAsync("/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task When_The_Request_Was_Cancelled_Should_Not_Turn_It_Into_An_Error_Response()
    {
        using var host = await StartAsync(new LogRecorder(), endpoints => endpoints.MapGet("/slow", string () => throw new OperationCanceledException()).WithExceptionHandling());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.GetTestClient().GetAsync("/slow"));
    }

    [Fact]
    public async Task When_The_Group_Has_The_Filter_Should_Handle_Every_Endpoint_In_It()
    {
        using var host = await StartAsync(new LogRecorder(), endpoints =>
        {
            var group = endpoints.MapGroup("/api").WithExceptionHandling();
            group.MapGet("/a", string () => throw new KeyNotFoundException("a"));
            group.MapGet("/b", string () => throw new KeyNotFoundException("b"));
        });
        var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/a")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/b")).StatusCode);
    }

    [Fact]
    public async Task When_The_Body_Is_Invalid_Should_Answer_400_With_The_Fields_And_Not_Call_The_Endpoint()
    {
        var calls = 0;
        using var host = await StartAsync(new LogRecorder(), endpoints => endpoints.MapPost("/products", (NewProduct product) =>
        {
            calls++;
            return Results.Created("/products/1", product);
        }).WithValidation<NewProduct>());

        var response = await host.GetTestClient().PostAsJsonAsync("/products", new NewProduct(string.Empty, -1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"field\": \"Name\"", body);
        Assert.Contains("\"field\": \"Price\"", body);
        Assert.Contains("\"errorCode\": 400", body);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task When_The_Body_Is_Valid_Should_Call_The_Endpoint()
    {
        using var host = await StartAsync(new LogRecorder(), endpoints => endpoints.MapPost("/products", (NewProduct product) => Results.Created("/products/1", product)).WithValidation<NewProduct>());

        var response = await host.GetTestClient().PostAsJsonAsync("/products", new NewProduct("Teapot", 129m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task When_Several_Validators_Are_Registered_Should_Report_The_Failures_Of_All_Of_Them()
    {
        using var host = await StartAsync(new LogRecorder(), endpoints => endpoints.MapPost("/products", (NewProduct product) => Results.Ok()).WithValidation<NewProduct>());

        var response = await host.GetTestClient().PostAsJsonAsync("/products", new NewProduct("reserved", 0));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"field\": \"Price\"", body);   // from the first validator
        Assert.Contains("\"field\": \"Name\"", body);    // from the second one
    }

    [Fact]
    public async Task When_The_Group_Validates_A_Type_Should_Pass_Endpoints_Without_That_Argument()
    {
        using var host = await StartAsync(new LogRecorder(), endpoints =>
        {
            var group = endpoints.MapGroup("/api").WithValidation<NewProduct>();
            group.MapGet("/ping", () => "pong");
            group.MapPost("/products", (NewProduct product) => Results.Ok());
        });
        var client = host.GetTestClient();

        Assert.Equal("pong", await client.GetStringAsync("/api/ping"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/products", new NewProduct(string.Empty, 1))).StatusCode);
    }

    [Fact]
    public async Task When_No_Validator_Is_Registered_For_The_Type_Should_Call_The_Endpoint()
    {
        using var host = await StartAsync(new LogRecorder(), endpoints => endpoints.MapPost("/notes", (Note note) => Results.Ok(note.Text)).WithValidation<Note>());

        var response = await host.GetTestClient().PostAsJsonAsync("/notes", new Note(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public record Note(string Text);

    [Theory]
    [InlineData(404, 404)]
    [InlineData(503, 503)]
    [InlineData(200, 500)]   // an error model must never be answered with a success status
    [InlineData(0, 500)]
    public async Task When_A_Model_Becomes_A_Result_Should_Use_Its_Code_As_The_Status(int errorCode, int expectedStatus)
    {
        using var host = await StartAsync(new LogRecorder(), endpoints => endpoints.MapGet("/model", () =>
            new ExceptionHandlingResultModel(errorCode, "message", [new ExceptionOrValidationError("Field", "message")]).ToResult()));

        var response = await host.GetTestClient().GetAsync("/model");

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Contains("\"field\": \"Field\"", await response.Content.ReadAsStringAsync());
    }

    private static async Task<IHost> StartAsync(LogRecorder log, Action<IEndpointRouteBuilder> map)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment(Environments.Production)
                .ConfigureLogging(logging => logging.ClearProviders().SetMinimumLevel(LogLevel.Debug).AddProvider(log))
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddExceptionHandlingForMinimalApi<IExceptionHandlingProvider, NotFoundAwareProvider>();
                    services.AddTransient<IValidator<NewProduct>, NewProductValidator>();
                    services.AddTransient<IValidator<NewProduct>, NameIsNotReserved>();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(map);
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
