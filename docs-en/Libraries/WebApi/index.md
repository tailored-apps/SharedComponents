# TailoredApps.Shared.WebApi

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.WebApi)](https://www.nuget.org/packages/TailoredApps.Shared.WebApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

Production-ready defaults for ASP.NET Core Web APIs — a single call wires up the cross-cutting concerns every modern API needs, so each service starts with the same consistent observability, health-probing and resilient HTTP client configuration.

`AddTailoredWebApiDefaults` configures:

- **Metrics, tracing and logging** — OpenTelemetry for ASP.NET Core, `HttpClient` and the .NET runtime, exported over **OTLP**
- **Health checks** — readiness (`/health`, all checks) and liveness (`/alive`, checks tagged `live`)
- **HTTP clients** — the standard resilience handler (retry, circuit breaker, timeouts) and service discovery on every factory-created client

Every feature binds from the `WebApiDefaults` configuration section and can be toggled independently. OTLP is the default exporter (stable); a `ConfigureMetrics` hook is provided for opting into a Prometheus scrape endpoint.

---

## Installation

```bash
dotnet add package TailoredApps.Shared.WebApi
```

---

## DI Registration

```csharp
// Program.cs
using TailoredApps.Shared.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Registers OpenTelemetry, health checks and resilient HTTP clients
builder.AddTailoredWebApiDefaults();

var app = builder.Build();

// Maps the /health (readiness) and /alive (liveness) endpoints
app.MapTailoredWebApiDefaults();

app.Run();
```

---

## Configuration

Everything can be driven from the `WebApiDefaults` section in `appsettings.json`:

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
      "EnableMetrics": true,
      "EnableTracing": true,
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

The OTLP endpoint also honours the standard `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable. When no endpoint is resolved, telemetry is still collected in-process but not exported anywhere.

Code overrides take precedence over configuration:

```csharp
builder.AddTailoredWebApiDefaults(options =>
{
    options.ServiceName = "catalog-api";
    options.HealthChecks.AllowDetailedResponse = builder.Environment.IsDevelopment();
});
```

---

## Usage

### Kubernetes probes

Once the endpoints are mapped, point your probes at them:

```yaml
livenessProbe:
  httpGet:
    path: /alive
    port: 8080
readinessProbe:
  httpGet:
    path: /health
    port: 8080
```

### Prometheus `/metrics` endpoint (optional)

Metrics flow over OTLP by default. If you need a local Prometheus-scrapable endpoint, add the `OpenTelemetry.Exporter.Prometheus.AspNetCore` package and use the hook:

```csharp
builder.AddTailoredWebApiDefaults(options =>
{
    options.Observability.ConfigureMetrics = metrics => metrics.AddPrometheusExporter();
});

var app = builder.Build();
app.MapTailoredWebApiDefaults();
app.MapPrometheusScrapingEndpoint(); // /metrics
```

---

## API Reference

| Type | Kind | Description |
|------|------|-------------|
| `WebApiDefaultsExtensions.AddTailoredWebApiDefaults` | Ext. method | Registers OpenTelemetry, health checks and resilient HTTP clients |
| `WebApiDefaultsExtensions.MapTailoredWebApiDefaults` | Ext. method | Maps the `/health` (readiness) and `/alive` (liveness) endpoints |
| `WebApiDefaultsOptions` | Options | Root configuration (`WebApiDefaults` section) |
| `HealthCheckSettings` | Options | Health check paths and response detail |
| `ObservabilitySettings` | Options | Metrics, tracing, logging and the OTLP exporter |
| `HttpClientSettings` | Options | Resilience and service discovery for `HttpClient` |

---

## 🔒 Security

- **Retries only for idempotent methods.** The standard resilience handler (`AddStandardResilienceHandler`) used to retry `POST`/`PATCH` as well. A retried `POST /charge` whose first attempt reached the server is a duplicate charge. Retrying non-idempotent methods is now **disabled** (`HttpClientSettings.RetryUnsafeHttpMethods = false`). Enable it only when every HTTP client in the host uses idempotency keys.

```json
{ "WebApiDefaults": { "HttpClient": { "EnableStandardResilience": true, "RetryUnsafeHttpMethods": false } } }
```

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.WebApi — AI agent instructions

You are using the TailoredApps.Shared.WebApi library to wire production-ready Web API defaults (OpenTelemetry, health checks, HTTP resilience) with a single call.

### Registration
```csharp
// Program.cs
builder.AddTailoredWebApiDefaults();
var app = builder.Build();
app.MapTailoredWebApiDefaults(); // /health + /alive
```

### What gets configured
- Metrics + tracing + logging (OpenTelemetry) exported over OTLP
- Health checks: /health (readiness) and /alive (liveness)
- HttpClient: standard resilience handler + service discovery

### Configuration (WebApiDefaults section)
- ServiceName / ServiceVersion — OpenTelemetry resource attributes
- Observability.OtlpExporterEndpoint or the OTEL_EXPORTER_OTLP_ENDPOINT variable
- HealthChecks.ReadinessPath / LivenessPath / AllowDetailedResponse
- HttpClient.EnableStandardResilience / EnableServiceDiscovery

### Rules
- Call AddTailoredWebApiDefaults on the builder before Build(), and MapTailoredWebApiDefaults on the app after Build()
- Do not register OpenTelemetry or health checks manually — the library does it for you
- Use /alive as the liveness probe and /health as the readiness probe
- To expose Prometheus /metrics, use the Observability.ConfigureMetrics hook
- Do not enable `HttpClient:RetryUnsafeHttpMethods` without idempotency keys - a retried POST can duplicate a payment or order
```
