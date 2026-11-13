using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TailoredApps.AotSmoke.Web;
using TailoredApps.Shared.DateTime;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.Model;
using TailoredApps.Shared.ExceptionHandling.Providers;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;
using TailoredApps.Shared.WebApi;

// A Web API the way NativeAOT allows it - minimal APIs - on the libraries a Web API uses. The application
// starts on a free port, calls its own endpoints over HTTP and reports every check; the exit code is the
// number of failed checks. Published with NativeAOT it proves that nothing was trimmed away.

const string issuer = "https://smoke.example";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("smoke-not-a-real-signing-key-0123456789"));

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["WebApiDefaults:ServiceName"] = "aot-smoke",
    ["WebApiDefaults:HttpClient:EnableServiceDiscovery"] = "true",
    ["Mail:Providers:Smtp:Host"] = "127.0.0.1",
    ["Mail:Providers:Smtp:Port"] = "1",                 // nothing listens there
    ["Mail:Providers:Smtp:From"] = "shop@demo.example",
    ["Mail:Providers:Smtp:IsProd"] = "true",
});

builder.AddTailoredWebApiDefaults();
builder.Services.AddDateTimeProvider();
builder.Services.AddExceptionHandlingForMinimalApi<IExceptionHandlingProvider, SmokeExceptionProvider>();
builder.Services.AddTransient<IValidator<NewProduct>, NewProductValidator>();
builder.Services.RegisterConsoleProvider();
builder.Services.AddTransient<SmtpEmailProvider>();   // by its own type, next to the console provider
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, SmokeJsonContext.Default));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = issuer,
        ValidAudience = "smoke",
        IssuerSigningKey = signingKey,
        NameClaimType = "name",
    };
});
builder.Services.AddAuthorizationBuilder().AddPolicy("orders", policy => policy.RequireClaim("scope", "orders"));

var app = builder.Build();

app.ConfigureExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapTailoredWebApiDefaults();

app.MapGet("/public", (IDateTimeProvider clock) => new Answer("public", clock.OffsetUtcNow.Year));
app.MapGet("/orders", [Authorize(Policy = "orders")] (ClaimsPrincipal user) => new Answer(user.Identity?.Name ?? "?", 0));
app.MapGet("/missing", Answer () => throw new KeyNotFoundException("Order 7 does not exist."));
app.MapGet("/invalid", Answer () => throw new ValidationException([new FluentValidation.Results.ValidationFailure("Id", "'Id' must be greater than '0'.")]));
app.MapGet("/boom", Answer () => throw new InvalidOperationException("Server=db;Password=secret"));
// ExceptionHandling.MinimalApi: the same answers from endpoint filters, with a real FluentValidation validator.
var filtered = app.MapGroup("/filtered").WithExceptionHandling();
filtered.MapGet("/missing", Answer () => throw new KeyNotFoundException("Product 7 does not exist."));
filtered.MapPost("/products", (NewProduct product) => new Answer(product.Name, 1)).WithValidation<NewProduct>();

app.MapPost("/mail", async (IEmailProvider email, IMailMessageBuilder messages) =>
{
    var body = messages.Build(
        "order.html",
        new Dictionary<string, string> { ["Number"] = "ORD-42 <b>" },
        new Dictionary<string, string> { ["order.html"] = "<p>Order {{Number}} is paid.</p>" });
    return new Answer(await email.SendMail("anna@demo.example", "Order ORD-42", body, null), 0);
});

await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
using var client = new HttpClient { BaseAddress = new Uri(address) };

var failures = 0;

void Check(string name, bool passed, string? detail = null)
{
    Console.WriteLine($"{(passed ? "ok  " : "FAIL")} {name}{(passed || detail is null ? string.Empty : $" - {detail}")}");
    if (!passed)
        failures++;
}

async Task Expect(string name, HttpRequestMessage request, HttpStatusCode status, params string[] fragments)
{
    try
    {
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var missing = fragments.Where(f => !body.Contains(f, StringComparison.Ordinal)).ToArray();
        Check(name, response.StatusCode == status && missing.Length == 0, $"{(int)response.StatusCode} {body}");
    }
    catch (Exception exception)
    {
        Check(name, false, $"{exception.GetType().Name}: {exception.Message}");
    }
}

HttpRequestMessage Get(string path, string? token = null)
{
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    if (token is not null)
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return request;
}

string Token(params Claim[] claims) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
{
    Issuer = issuer,
    Audience = "smoke",
    Subject = new ClaimsIdentity(claims),
    Expires = DateTime.UtcNow.AddMinutes(5),
    SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
});

// WebApi: health endpoints.
await Expect("webapi: /health answers Healthy", Get("/health"), HttpStatusCode.OK, "Healthy");
await Expect("webapi: /alive answers Healthy", Get("/alive"), HttpStatusCode.OK, "Healthy");

// A minimal-API endpoint with a service from the container and a JSON answer.
await Expect("endpoint: JSON answer with the clock", Get("/public"), HttpStatusCode.OK, "\"name\":\"public\"", $"\"value\":{DateTime.UtcNow.Year}");

// [Authorize] with a JWT: no token, a token without the scope, a token with it.
await Expect("authorize: no token is 401", Get("/orders"), HttpStatusCode.Unauthorized);
await Expect("authorize: a token without the scope is 403", Get("/orders", Token(new Claim("name", "anna"))), HttpStatusCode.Forbidden);
await Expect("authorize: a token with the scope is 200", Get("/orders", Token(new Claim("name", "anna"), new Claim("scope", "orders"))), HttpStatusCode.OK, "\"name\":\"anna\"");
await Expect("authorize: a token signed with another key is 401",
    Get("/orders", new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = issuer,
        Audience = "smoke",
        Subject = new ClaimsIdentity([new Claim("scope", "orders")]),
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-another-key-another-key-0123")), SecurityAlgorithms.HmacSha256),
    })),
    HttpStatusCode.Unauthorized);

// ExceptionHandling: the middleware turns exceptions into the JSON error model.
await Expect("errors: an exception mapped by the provider is 404", Get("/missing"), HttpStatusCode.NotFound, "Order 7 does not exist.", "\"errorCode\": 404");
await Expect("errors: a validation failure is 400 with the field", Get("/invalid"), HttpStatusCode.BadRequest, "\"field\": \"Id\"");
{
    using var response = await client.SendAsync(Get("/boom"));
    var body = await response.Content.ReadAsStringAsync();
    Check("errors: an unexpected exception is a generic 500", response.StatusCode == HttpStatusCode.InternalServerError && !body.Contains("secret", StringComparison.Ordinal), body);
}

// ExceptionHandling.MinimalApi: endpoint filters.
await Expect("filters: an exception in the group is answered by the filter", Get("/filtered/missing"), HttpStatusCode.NotFound, "Product 7 does not exist.");
await Expect("filters: an invalid body is 400 with every invalid field",
    new HttpRequestMessage(HttpMethod.Post, "/filtered/products") { Content = new StringContent("""{"name":"","price":-1}""", Encoding.UTF8, "application/json") },
    HttpStatusCode.BadRequest, "\"field\": \"Name\"", "\"field\": \"Price\"");
await Expect("filters: a valid body reaches the endpoint",
    new HttpRequestMessage(HttpMethod.Post, "/filtered/products") { Content = new StringContent("""{"name":"Teapot","price":129}""", Encoding.UTF8, "application/json") },
    HttpStatusCode.OK, "\"name\":\"Teapot\"");

// Email: a template with an HTML-encoded variable, sent through the console provider.
await Expect("email: the template is rendered and the variable encoded", new HttpRequestMessage(HttpMethod.Post, "/mail"), HttpStatusCode.OK, "Order ORD-42 &lt;b&gt; is paid.");

// Email over SMTP: nothing listens on the port, so sending has to fail - with the SMTP client's own error,
// which shows the provider and System.Net.Mail are intact in the native binary.
try
{
    await app.Services.GetRequiredService<SmtpEmailProvider>().SendMail("anna@demo.example", "Order ORD-42", "<p>paid</p>", null);
    Check("email: the SMTP provider reaches the SMTP client", false, "no exception");
}
catch (System.Net.Mail.SmtpException)
{
    Check("email: the SMTP provider reaches the SMTP client", true);
}
catch (Exception exception)
{
    Check("email: the SMTP provider reaches the SMTP client", false, $"{exception.GetType().Name}: {exception.Message}");
}

// WebApi: an HTTP client from the factory carries the resilience handler and service discovery.
try
{
    using var resilient = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("smoke");
    using var response = await resilient.GetAsync(new Uri(new Uri(address), "/public"));
    Check("webapi: a factory client with resilience reaches the application", response.IsSuccessStatusCode, ((int)response.StatusCode).ToString());
}
catch (Exception exception)
{
    Check("webapi: a factory client with resilience reaches the application", false, $"{exception.GetType().Name}: {exception.Message}");
}

await app.StopAsync();
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures;

namespace TailoredApps.AotSmoke.Web
{
    public record Answer(string Name, int Value);

    public record NewProduct(string Name, decimal Price);

    public sealed class NewProductValidator : AbstractValidator<NewProduct>
    {
        public NewProductValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Price).GreaterThan(0);
        }
    }

    /// <summary>The answers of the endpoints, serialized without reflection as NativeAOT requires.</summary>
    [JsonSerializable(typeof(Answer))]
    [JsonSerializable(typeof(NewProduct))]
    internal sealed partial class SmokeJsonContext : JsonSerializerContext;

    /// <summary>The defaults of ExceptionHandling, plus the 404 mapping an application typically adds.</summary>
    public sealed class SmokeExceptionProvider : IExceptionHandlingProvider
    {
        private readonly DefaultExceptionHandlingProvider defaults = new();

        public ExceptionHandlingResultModel Response(Exception exception)
            => exception is KeyNotFoundException
                ? new ExceptionHandlingResultModel(404, exception.Message, [new ExceptionOrValidationError(string.Empty, exception.Message)])
                : defaults.Response(exception);

        public ExceptionHandlingResultModel Response(ModelStateDictionary modelState) => defaults.Response(modelState);
    }
}
