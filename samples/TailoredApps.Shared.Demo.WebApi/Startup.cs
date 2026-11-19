using System.Text;
using FluentValidation;
using ForkForward.Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using TailoredApps.Demo.Services;
using TailoredApps.Demo.Services.Catalog.Infrastructure;
using TailoredApps.Demo.Services.Catalog.Interfaces.Infrastructure;
using TailoredApps.Demo.Services.Catalog.Processing;
using TailoredApps.Demo.Services.Internals;
using TailoredApps.Demo.Services.Orders.Infrastructure;
using TailoredApps.Demo.Services.Orders.Interfaces.Infrastructure;
using TailoredApps.Demo.Services.Orders.Processing;
using TailoredApps.Demo.WebApi.Authentication;
using TailoredApps.Demo.WebApi.Endpoints;
using TailoredApps.Demo.WebApi.ExceptionHandling;
using TailoredApps.Demo.WebApi.Payments;
using TailoredApps.Shared.DateTime;
using TailoredApps.Shared.Email;
using TailoredApps.Shared.Email.MailMessageBuilder;
using TailoredApps.Shared.Email.Office365;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Middleware;
using TailoredApps.Shared.Mediator.DI;
using TailoredApps.Shared.Mediator.Email.Handlers;
using TailoredApps.Shared.Mediator.ImageClassification.Infrastructure;
using TailoredApps.Shared.Mediator.ImageClassification.Interfaces.Domain.Models;
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

namespace TailoredApps.Demo.WebApi;

/// <summary>The composition of the service: what is registered, and in which order a request passes through it.</summary>
public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        // The key the tokens of this service are verified with. Fail at start rather than on the first request.
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < JwtOptions.MinimumKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:Key must be the token signing key, at least {JwtOptions.MinimumKeyLength} characters (locally: appsettings.Development.json; on a server: a secret).");
        }

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));

        // ── DateTime: one clock (TimeProvider) for the whole service ────────────────────────────────────────
        services.AddDateTimeProvider();

        // ── EntityFramework: a unit of work over the data context, committed or rolled back per request ─────
        // An in-memory SQLite database lives as long as its connection, so one open connection is shared. This
        // also keeps the unit of work from sending its SQL Server "SET TRANSACTION ISOLATION LEVEL" statement,
        // which it issues when a connection opens and which SQLite does not understand.
        services.AddSingleton(_ =>
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            return connection;
        });
        services.AddDbContext<ShopDataContext>((provider, options) => options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
        services.AddUnitOfWorkForWebApi<IShopDataContext, ShopDataContext>();

        // ── ExceptionHandling: one JSON error shape; the service's own exceptions decide the status ─────────
        services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, ShopExceptionHandlingProvider>();

        services.AddControllers(options =>
        {
            options.Filters.AddUnitOfWorkTransactionAttribute();
            options.Filters.AddExceptionHandlingFilterAttribute();

            // Requests are validated in one place, by their FluentValidation validators in the mediator
            // pipeline. Without this, MVC would also treat every non-nullable string of a message as required
            // before the action runs - including the ones an action fills from the route.
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        });

        // ── Authentication: JWT bearer tokens of this issuer and audience, signed with Jwt:Key ──────────────
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
            };
        });
        services.AddAuthorization();

        // ── The service's own registrations ─────────────────────────────────────────────────────────────────
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddSingleton<RecommendationSource>();
        services.AddSingleton<HandlerCallCounter>();
        services.AddSingleton<ShopMetrics>();
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(ShopMetrics.MeterName));

        // ── ForkForward.Mapper: the profiles of the Services assembly ───────────────────────────────────────
        services.AddAutoMapper(typeof(ShopDataContext).Assembly);

        // ── ForkForward.Mediator and the Mediator libraries ──────────────────────────────────────────────────
        var imageModel = configuration[ImageClassificationOptions.ModelFilePathConfig];
        var imageClassification = !string.IsNullOrWhiteSpace(imageModel) && File.Exists(imageModel);

        services.AddMediator(cfg =>
        {
            // Scanning finds request, stream and notification handlers, exception handlers and exception actions.
            cfg.RegisterServicesFromAssembly(typeof(ShopDataContext).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(SendMailCommandHandler).Assembly);          // Mediator.Email
            if (imageClassification)
                cfg.RegisterServicesFromAssembly(typeof(AddPredictionEngineExtension).Assembly); // Mediator.ML

            // It does not find pre- and post-processors or stream behaviors: these are named.
            cfg.AddRequestPreProcessor<NormalizeCustomerEmail>();
            cfg.AddRequestPostProcessor<CountStartedPayments>();
            cfg.AddStreamBehavior<CountExportedProducts>();

            // OrderPaid has two handlers. The default publisher awaits them one after another, which is what
            // handlers sharing the request's data context need; TaskWhenAllPublisher would start them together.
            cfg.NotificationPublisher = new ForkForward.Mediator.NotificationPublishers.ForeachAwaitPublisher();
        });

        // Registered after AddMediator, so the order of a request is: exception handlers and actions ->
        // pre-processors -> post-processors -> Logging -> Validation -> Caching -> Fallback -> Retry -> handler.
        var pipeline = new PipelineRegistration(services);
        pipeline.RegisterPipelineBehaviors();                                   // the behaviors and HybridCache
        pipeline.RegisterPipelineBehaviors(typeof(ShopDataContext).Assembly);   // cache policies, fallback handlers, retry settings

        // The validators the validation behavior (and the minimal-API validation filter) runs.
        services.AddValidatorsFromAssemblyContaining<ShopDataContext>();

        // ── Mediator.ML: only with a trained model ───────────────────────────────────────────────────────────
        if (imageClassification)
        {
            services.AddPredictionEngine(engine =>
                engine.RegisterMachineLearningModel<InMemoryImageData, ImagePredictionScore>(pool => pool.FromFile(imageModel!)));
        }

        // ── Email: messages built from templates; the console provider prints them instead of sending ───────
        services.RegisterConsoleProvider();
        services.Configure<TokenReplacingMailMessageBuilderOptions>(options =>
        {
            options.Location = Path.Combine(environment.ContentRootPath, "EmailTemplates");
            options.FileExtension = "html";
        });
        services.AddTransient<IMailMessageBuilder, TokenReplacingMailMessageBuilder>();

        // ── Email.Office365: reading a mailbox, only with an Azure AD application configured ────────────────
        if (configuration.GetValue<bool>("Demo:Office365Inbox"))
        {
            services.AddOptions<AuthenticationConfig>();
            services.ConfigureOptions<Office365EmailConfigureOptions>();
            services.AddTransient<Office365EmailProvider>();   // by its own type: IEmailProvider stays the sender above
        }

        // ── Payments: every provider, one call each ─────────────────────────────────────────────────────────
        services.AddPayments()
            .RegisterAdyenProvider()
            .RegisterCashbillProvider()
            .RegisterHotPayProvider()
            .RegisterPayNowProvider()
            .RegisterPayUProvider()
            .RegisterPrzelewy24Provider()
            .RegisterRevolutProvider()
            .RegisterStripeProvider()
            .RegisterTpayProvider();

        // The sample has no account at any gateway: the providers' HTTP clients talk to a fake that answers
        // like the gateways do.
        services.AddSingleton<FakeGatewayState>();
        if (configuration.GetValue("Demo:UseFakeGateways", true))
        {
            foreach (var client in FakeGatewayHandler.ClientNames)
            {
                services.AddHttpClient(client)
                    .ConfigurePrimaryHttpMessageHandler(provider => new FakeGatewayHandler(provider.GetRequiredService<FakeGatewayState>()));
            }

            // Stripe talks through its SDK. Its services take an IStripeClient when the container has one, so
            // this client sends them to the fake as well; without it they would call api.stripe.com.
            services.AddSingleton<Stripe.IStripeClient>(provider => new Stripe.StripeClient(
                apiKey: provider.GetRequiredService<IOptions<StripeServiceOptions>>().Value.SecretKey,
                httpClient: new Stripe.SystemNetHttpClient(provider.GetRequiredService<IHttpClientFactory>().CreateClient("Stripe")),
                apiBase: $"{FakeGatewayHandler.BaseUrl}/stripe"));
        }
    }

    public void Configure(WebApplication app)
    {
        app.ConfigureExceptionHandler();     // ExceptionHandling middleware: whatever the filters did not answer

        // The browser client (Demo.Client, Blazor WebAssembly) is served from here: its runtime files, then its
        // static files. Routing comes after them on purpose: once routing has picked an endpoint (the
        // client's fallback below matches almost any address) the static file middleware steps aside.
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapProductEndpoints();           // minimal APIs: the same messages through endpoint filters

        // An address that is neither a file nor part of the API belongs to the client's router. An unknown
        // address under /api stays a 404 instead of answering with the page: of two fallbacks the more
        // specific pattern wins.
        app.MapFallbackToFile("index.html");
        app.MapFallback("/api/{**path}", () => Results.NotFound());

        // Tokens come from the organisation's auth service and payment confirmations from the gateways.
        // Locally these endpoints stand in for both.
        if (environment.IsDevelopment())
        {
            app.MapDevelopmentGatewayEndpoint();
            app.MapDevelopmentTokenEndpoint();
        }
    }
}
