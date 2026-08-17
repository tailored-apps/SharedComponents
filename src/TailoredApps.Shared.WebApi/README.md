# TailoredApps.Shared.WebApi

Production-ready defaults for ASP.NET Core Web APIs, wired through a single call. Add this package to
every service and get consistent observability, health probing and outbound-call resilience without
copy-pasting boilerplate into each `Program.cs`.

## What you get

| Concern | What is configured |
| --- | --- |
| **Metrics** | OpenTelemetry metrics for ASP.NET Core, `HttpClient` and the .NET runtime (GC, thread pool, exceptions). |
| **Tracing** | OpenTelemetry distributed tracing for ASP.NET Core and `HttpClient`; health-probe requests are filtered out. |
| **Logging** | OpenTelemetry logging with formatted messages and scopes. |
| **Export** | OTLP exporter for all three signals, enabled when an endpoint is configured. |
| **Health checks** | Readiness (`/health`, all checks) and liveness (`/alive`, `live`-tagged checks) endpoints with a JSON writer. |
| **HTTP clients** | Standard resilience handler (retry, circuit breaker, timeouts) and service discovery on every client. |

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddTailoredWebApiDefaults();

var app = builder.Build();

app.MapTailoredWebApiDefaults(); // maps /health and /alive
app.Run();
```

### Configuration

Everything can be driven from the `WebApiDefaults` configuration section:

```json
{
  "WebApiDefaults": {
    "ServiceName": "catalog-api",
    "ServiceVersion": "1.4.0",
    "HealthChecks": {
      "ReadinessPath": "/health",
      "LivenessPath": "/alive",
      "AllowDetailedResponse": false
    },
    "Observability": {
      "EnableTracing": true,
      "EnableMetrics": true,
      "EnableLogging": true,
      "OtlpExporterEndpoint": "http://otel-collector:4317"
    },
    "HttpClient": {
      "EnableStandardResilience": true,
      "EnableServiceDiscovery": true
    }
  }
}
```

The OTLP endpoint also honours the standard `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable. When no
endpoint is resolved, telemetry is still collected in-process but not exported.

### Overriding in code

Code overrides are applied after configuration binding:

```csharp
builder.AddTailoredWebApiDefaults(options =>
{
    options.ServiceName = "catalog-api";
    options.HealthChecks.AllowDetailedResponse = builder.Environment.IsDevelopment();
});
```

### Adding a Prometheus `/metrics` endpoint

The default export path is OTLP (push to an OpenTelemetry Collector, which can expose Prometheus). If you
want an in-process Prometheus scrape endpoint instead, add the
`OpenTelemetry.Exporter.Prometheus.AspNetCore` package and use the metrics hook:

```csharp
builder.AddTailoredWebApiDefaults(options =>
{
    options.Observability.ConfigureMetrics = metrics => metrics.AddPrometheusExporter();
});

var app = builder.Build();
app.MapTailoredWebApiDefaults();
app.MapPrometheusScrapingEndpoint(); // /metrics
```

## Target frameworks

`net8.0`, `net9.0`, `net10.0`.
