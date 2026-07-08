# Shared.WebApi

`TailoredApps.Shared.WebApi` wires a consistent set of production-ready defaults into an ASP.NET Core
Web API through a single call, so every service ships with the same observability, health probing and
outbound-call resilience.

## What it configures

- **Metrics** &#8212; OpenTelemetry metrics for ASP.NET Core, `HttpClient` and the .NET runtime.
- **Tracing** &#8212; OpenTelemetry distributed tracing for ASP.NET Core and `HttpClient`; health-probe
  requests are filtered out so they do not flood the trace backend.
- **Logging** &#8212; OpenTelemetry logging with formatted messages and scopes.
- **Export** &#8212; an OTLP exporter for all three signals, enabled when an endpoint is configured
  (option `OtlpExporterEndpoint` or the standard `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable).
- **Health checks** &#8212; readiness (`/health`, all checks) and liveness (`/alive`, `live`-tagged
  checks) endpoints with a JSON response writer.
- **HTTP clients** &#8212; the standard resilience handler (retry, circuit breaker, timeouts) and service
  discovery applied to every client created by the factory.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddTailoredWebApiDefaults();

var app = builder.Build();

app.MapTailoredWebApiDefaults(); // maps /health and /alive
app.Run();
```

## Configuration

Bind from the `WebApiDefaults` section, or override in code (code wins over configuration):

```json
{
  "WebApiDefaults": {
    "ServiceName": "catalog-api",
    "Observability": {
      "OtlpExporterEndpoint": "http://otel-collector:4317"
    }
  }
}
```

Every feature can be toggled independently through `HealthChecks`, `Observability` and `HttpClient`
sub-sections. See the package README for the full option set and for enabling a Prometheus `/metrics`
scrape endpoint via the metrics hook.
