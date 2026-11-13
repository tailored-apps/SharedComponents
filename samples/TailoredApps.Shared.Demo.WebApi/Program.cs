using TailoredApps.Demo.Services;
using TailoredApps.Shared.WebApi;

namespace TailoredApps.Demo.WebApi;

public class Program
{
    public static void Main(string[] args)
    {
        var app = BuildApp(args);
        PrepareDatabase(app);
        app.Run();
    }

    /// <summary>
    /// Builds the application on the minimal-hosting model. The services and the request pipeline are
    /// configured in <see cref="Startup"/>; the shared Web API defaults (OpenTelemetry, health checks,
    /// resilient HTTP clients) are layered around it here.
    /// </summary>
    public static WebApplication BuildApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // A scoped service resolved from the root container is a bug in every environment, not only in Development.
        builder.Host.UseDefaultServiceProvider(options => options.ValidateScopes = true);

        // WebApi: bound from the "WebApiDefaults" configuration section.
        builder.AddTailoredWebApiDefaults();

        var startup = new Startup(builder.Configuration, builder.Environment);
        startup.ConfigureServices(builder.Services);

        var app = builder.Build();
        startup.Configure(app);

        app.MapTailoredWebApiDefaults();   // /health and /alive
        return app;
    }

    /// <summary>
    /// Creates the schema and the sample catalogue. A service with a real database applies its migrations
    /// here instead.
    /// </summary>
    private static void PrepareDatabase(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        ShopDataContext.Seed(scope.ServiceProvider.GetRequiredService<ShopDataContext>());

        // A mapping the profiles forgot fails here, at start, with the member named - not at the first request.
        scope.ServiceProvider.GetRequiredService<ForkForward.Mapper.IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
    }
}
