var builder = DistributedApplication.CreateBuilder(args);

// The Web API (which serves the browser client) targets net10.0 and net11.0, and `dotnet run` - what
// AddProject starts a project with - refuses a project with several frameworks unless it is told which
// one. So the service is started as an executable with the framework named. The other one:
//   dotnet run --project samples/TailoredApps.Shared.Demo.AppHost -- --Shop:Framework net10.0
var framework = builder.Configuration["Shop:Framework"] ?? "net11.0";

// The address people open. Another stack on the same machine may already use the port:
//   dotnet run --project samples/TailoredApps.Shared.Demo.AppHost -- --Shop:Port 5180
var port = int.TryParse(builder.Configuration["Shop:Port"], out var configured) ? configured : 5080;
var webApi = new Projects.TailoredApps_Shared_Demo_WebApi().ProjectPath;

builder.AddExecutable("shop", "dotnet", Path.GetDirectoryName(webApi)!, "run", "--project", webApi, "--framework", framework, "--no-launch-profile")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    // The process itself listens on a port Aspire picks; Aspire forwards the address above to it.
    .WithHttpEndpoint(port: port, env: "ASPNETCORE_HTTP_PORTS")
    .WithHttpHealthCheck("/health")
    // Points the OpenTelemetry export of the WebApi library at the dashboard: logs, traces and metrics.
    .WithOtlpExporter()
    .WithExternalHttpEndpoints();

builder.Build().Run();
