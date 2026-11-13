using FluentValidation;
using ForkForward.Mediator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;
using TailoredApps.Demo.Catalog;
using TailoredApps.Demo.Data;
using TailoredApps.Demo.Infrastructure;
using TailoredApps.Demo.Payments;
using TailoredApps.Shared.DateTime;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.Email.Office365;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;
using TailoredApps.Shared.MediatR.DI;
using TailoredApps.Shared.MediatR.Email.Handlers;
using TailoredApps.Shared.MediatR.ImageClassification.Infrastructure;
using TailoredApps.Shared.MediatR.ImageClassification.Interfaces.Domain.Models;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Adyen;
using TailoredApps.Shared.Payments.Provider.CashBill;
using TailoredApps.Shared.Payments.Provider.HotPay;
using TailoredApps.Shared.Payments.Provider.PayNow;
using TailoredApps.Shared.Payments.Provider.PayU;
using TailoredApps.Shared.Payments.Provider.Przelewy24;
using TailoredApps.Shared.Payments.Provider.Revolut;
using TailoredApps.Shared.Payments.Provider.Stripe;
using TailoredApps.Shared.Payments.Provider.Tpay;
using TailoredApps.Shared.WebApi;

var builder = WebApplication.CreateBuilder(args);

// ── WebApi: OpenTelemetry, health checks, HTTP client resilience ────────────────────────────────────────────
builder.AddTailoredWebApiDefaults();

// ── DateTime: one clock (TimeProvider) for the whole application ────────────────────────────────────────────
builder.Services.AddDateTimeProvider();

// ── ExceptionHandling: exceptions and invalid models become one JSON error shape ────────────────────────────
builder.Services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DemoExceptionHandlingProvider>();

// ── EntityFramework: a unit of work over the DbContext, committed or rolled back per request ────────────────
// An in-memory SQLite database lives as long as its connection, so one open connection is shared. This also
// keeps the unit of work from sending its SQL Server "SET TRANSACTION ISOLATION LEVEL" statement, which it
// issues when a connection opens and which SQLite does not understand.
builder.Services.AddSingleton(_ =>
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();
    return connection;
});
builder.Services.AddDbContext<ShopDbContext>((services, options) => options.UseSqlite(services.GetRequiredService<SqliteConnection>()));
builder.Services.AddUnitOfWorkForWebApi<IShopDbContext, ShopDbContext>();

builder.Services.AddControllers(options =>
{
    options.Filters.AddUnitOfWorkTransactionAttribute();
    options.Filters.AddExceptionHandlingFilterAttribute();
});

// ── MediatR libraries on ForkForward.Mediator: logging, validation, caching, fallback, retry ────────────────
var imageModel = builder.Configuration[ImageClassificationOptions.ModelFilePathConfig];
var imageClassification = !string.IsNullOrWhiteSpace(imageModel) && File.Exists(imageModel);

builder.Services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(SendMailCommandHandler).Assembly);          // MediatR.Email
    if (imageClassification)
        cfg.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).Assembly); // MediatR.ML
});

var pipeline = new PipelineRegistration(builder.Services);
pipeline.RegisterPipelineBehaviors();                           // the behaviours and HybridCache
pipeline.RegisterPipelineBehaviors(typeof(Program).Assembly);   // cache policies, fallback handlers, retry settings

builder.Services.AddTransient<IValidator<GetProducts>, GetProductsValidator>();
builder.Services.AddTransient<IValidator<GetProduct>, GetProductValidator>();
builder.Services.AddTransient<IValidator<NewProduct>, NewProductValidator>();   // used by the minimal-API validation filter
builder.Services.AddSingleton<HandlerCallCounter>();
builder.Services.AddSingleton<RecommendationSource>();

// ── MediatR.ML: only with a trained model ───────────────────────────────────────────────────────────────────
if (imageClassification)
{
    builder.Services.AddPredictionEngine(engine =>
        engine.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(pool => pool.FromFile(imageModel!)));
}

// ── Email: messages built from templates; the console provider prints them instead of sending ───────────────
builder.Services.RegisterConsoleProvider();
builder.Services.Configure<TokenReplacingMailMessageBuilderOptions>(options =>
{
    options.Location = Path.Combine(builder.Environment.ContentRootPath, "EmailTemplates");
    options.FileExtension = "html";
});
builder.Services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();

// ── Email.Office365: reading a mailbox, only with an Azure AD application configured ────────────────────────
if (builder.Configuration.GetValue<bool>("Demo:Office365Inbox"))
{
    builder.Services.AddOptions<AuthenticationConfig>();
    builder.Services.ConfigureOptions<Office365EmailConfigureOptions>();
    builder.Services.AddTransient<Office365EmailProvider>();   // by its own type: IEmailProvider stays the sender above
}

// ── Payments: every provider, one call each ─────────────────────────────────────────────────────────────────
builder.Services.AddPayments()
    .RegisterAdyenProvider()
    .RegisterCashbillProvider()
    .RegisterHotPayProvider()
    .RegisterPayNowProvider()
    .RegisterPayUProvider()
    .RegisterPrzelewy24Provider()
    .RegisterRevolutProvider()
    .RegisterStripeProvider()
    .RegisterTpayProvider();

// The demo has no account at any gateway: the providers' HTTP clients talk to a fake that answers like the
// gateways do.
builder.Services.AddSingleton<FakeGatewayState>();
if (builder.Configuration.GetValue("Demo:UseFakeGateways", true))
{
    foreach (var client in FakeGatewayHandler.ClientNames)
    {
        builder.Services.AddHttpClient(client)
            .ConfigurePrimaryHttpMessageHandler(services => new FakeGatewayHandler(services.GetRequiredService<FakeGatewayState>()));
    }

    // Stripe talks through its SDK. Its services take an IStripeClient when the container has one, so this
    // client sends them to the fake as well; without it they would call api.stripe.com.
    builder.Services.AddSingleton<Stripe.IStripeClient>(services => new Stripe.StripeClient(
        apiKey: services.GetRequiredService<IOptions<StripeServiceOptions>>().Value.SecretKey,
        httpClient: new Stripe.SystemNetHttpClient(services.GetRequiredService<IHttpClientFactory>().CreateClient("Stripe")),
        apiBase: $"{FakeGatewayHandler.BaseUrl}/stripe"));
}

var app = builder.Build();

app.ConfigureExceptionHandler();     // ExceptionHandling middleware
app.MapTailoredWebApiDefaults();     // /health and /alive
app.MapControllers();
app.MapProductEndpoints();          // minimal APIs: the same libraries through endpoint filters

using (var scope = app.Services.CreateScope())
{
    ShopDbContext.Seed(scope.ServiceProvider.GetRequiredService<ShopDbContext>());
}

app.Run();

/// <summary>Public so that the integration tests can start the application with WebApplicationFactory.</summary>
public partial class Program;
