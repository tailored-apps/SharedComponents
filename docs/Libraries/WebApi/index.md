# TailoredApps.Shared.WebApi

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.WebApi)](https://www.nuget.org/packages/TailoredApps.Shared.WebApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Opis

Produkcyjne defaulty dla Web API w ASP.NET Core — jednym wywołaniem podłącza komplet cross-cutting concerns wymaganych w nowoczesnym API, dzięki czemu każdy serwis startuje z tą samą, spójną konfiguracją obserwowalności, health checków i odpornych klientów HTTP.

`AddTailoredWebApiDefaults` konfiguruje:

- **Metryki, tracing i logi** — OpenTelemetry dla ASP.NET Core, `HttpClient` i runtime'u .NET, eksportowane przez **OTLP**
- **Health checks** — readiness (`/health`, wszystkie checki) i liveness (`/alive`, checki oznaczone tagiem `live`)
- **Klienty HTTP** — standardowy handler odporności (retry, circuit breaker, timeouty) oraz service discovery dla każdego klienta tworzonego przez fabrykę

Wszystkie funkcje bindują się z sekcji konfiguracji `WebApiDefaults` i można je włączać/wyłączać niezależnie. Domyślnym eksporterem jest OTLP (stabilny); dla scrape'owalnego endpointu Prometheus dostępny jest hook `ConfigureMetrics`.

---

## Instalacja

```bash
dotnet add package TailoredApps.Shared.WebApi
```

---

## Rejestracja w DI

```csharp
// Program.cs
using TailoredApps.Shared.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Rejestruje OpenTelemetry, health checks oraz odporne klienty HTTP
builder.AddTailoredWebApiDefaults();

var app = builder.Build();

// Mapuje endpointy /health (readiness) i /alive (liveness)
app.MapTailoredWebApiDefaults();

app.Run();
```

---

## Konfiguracja

Wszystko można wysterować z sekcji `WebApiDefaults` w `appsettings.json`:

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

Endpoint OTLP respektuje też standardową zmienną środowiskową `OTEL_EXPORTER_OTLP_ENDPOINT`. Gdy żaden endpoint nie zostanie ustawiony, telemetria jest zbierana w procesie, ale nie jest nigdzie wysyłana.

Nadpisania w kodzie mają pierwszeństwo przed konfiguracją:

```csharp
builder.AddTailoredWebApiDefaults(options =>
{
    options.ServiceName = "catalog-api";
    options.HealthChecks.AllowDetailedResponse = builder.Environment.IsDevelopment();
});
```

---

## Przykład użycia

### Sondy Kubernetes

Po zmapowaniu endpointów wystarczy wskazać je w probe'ach:

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

### Endpoint Prometheus `/metrics` (opcjonalnie)

Domyślnie metryki lecą przez OTLP. Jeśli potrzebujesz lokalnego endpointu scrape'owalnego przez Prometheus, dodaj pakiet `OpenTelemetry.Exporter.Prometheus.AspNetCore` i użyj hooka:

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

| Typ | Rodzaj | Opis |
|-----|--------|------|
| `WebApiDefaultsExtensions.AddTailoredWebApiDefaults` | Metoda ext. | Rejestruje OpenTelemetry, health checks i odporne klienty HTTP |
| `WebApiDefaultsExtensions.MapTailoredWebApiDefaults` | Metoda ext. | Mapuje endpointy `/health` (readiness) i `/alive` (liveness) |
| `WebApiDefaultsOptions` | Options | Konfiguracja główna (sekcja `WebApiDefaults`) |
| `HealthCheckSettings` | Options | Ścieżki i szczegółowość odpowiedzi health checków |
| `ObservabilitySettings` | Options | Metryki, tracing, logi oraz eksporter OTLP |
| `HttpClientSettings` | Options | Resilience i service discovery dla `HttpClient` |

---

## 🤖 AI Agent Prompt

```markdown
## TailoredApps.Shared.WebApi — Instrukcja dla agenta AI

Używasz biblioteki TailoredApps.Shared.WebApi, aby jednym wywołaniem podłączyć produkcyjne defaulty Web API (OpenTelemetry, health checks, resilience HTTP).

### Rejestracja
```csharp
// Program.cs
builder.AddTailoredWebApiDefaults();
var app = builder.Build();
app.MapTailoredWebApiDefaults(); // /health + /alive
```

### Co zostaje skonfigurowane
- Metryki + tracing + logi (OpenTelemetry) eksportowane przez OTLP
- Health checks: /health (readiness) i /alive (liveness)
- HttpClient: standardowy resilience handler + service discovery

### Konfiguracja (sekcja WebApiDefaults)
- ServiceName / ServiceVersion — atrybuty zasobu OpenTelemetry
- Observability.OtlpExporterEndpoint lub zmienna OTEL_EXPORTER_OTLP_ENDPOINT
- HealthChecks.ReadinessPath / LivenessPath / AllowDetailedResponse
- HttpClient.EnableStandardResilience / EnableServiceDiscovery

### Zasady
- Wywołaj AddTailoredWebApiDefaults na builderze przed Build(), a MapTailoredWebApiDefaults na aplikacji po Build()
- Nie rejestruj OpenTelemetry ani health checków ręcznie — biblioteka robi to za Ciebie
- /alive używaj jako liveness probe, /health jako readiness probe
- Aby wystawić Prometheus /metrics, użyj hooka Observability.ConfigureMetrics
```
