# TailoredApps.Shared.ExceptionHandling.MinimalApi

Exception handling and request validation for ASP.NET Core **minimal APIs** as endpoint filters, on top of
`TailoredApps.Shared.ExceptionHandling`: the same provider and the same JSON error model as the middleware and the
MVC filter. Safe for trimming and NativeAOT.

## Installation

```bash
dotnet add package TailoredApps.Shared.ExceptionHandling.MinimalApi
```

## Registration

```csharp
using FluentValidation;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.Providers;

builder.Services.AddExceptionHandlingForMinimalApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
builder.Services.AddTransient<IValidator<NewProduct>, NewProductValidator>();
```

## Usage

```csharp
var products = app.MapGroup("/api/products").WithExceptionHandling();

// An invalid body is answered with 400 and one error per field; the endpoint is not called.
products.MapPost("/", (NewProduct product) => Results.Created($"/api/products/{product.Name}", product))
        .WithValidation<NewProduct>();

// An exception is answered with the provider's error model.
products.MapGet("/{id:int}", (int id) => Find(id) ?? throw new KeyNotFoundException($"Product {id} does not exist."));
```

## Good to know

- `WithValidation<TRequest>()` runs every `IValidator<TRequest>` registered in the container and reports the failures of
  all of them. Without such an argument, or without a validator, the endpoint runs normally. The request type is a type
  parameter on purpose: no reflection, so the filter works under NativeAOT.
- `WithExceptionHandling()` is for an endpoint or a group. For the whole application the middleware
  `app.ConfigureExceptionHandler()` of `TailoredApps.Shared.ExceptionHandling` is enough. A cancelled request
  (`OperationCanceledException`) passes through.
- An exception answered with 4xx is logged at Debug level, one answered with 5xx as an error.
- Filters run in the order they are added; add `WithExceptionHandling()` first.
- `model.ToResult()` turns an `ExceptionHandlingResultModel` into an `IResult` with the model's status (400-599,
  otherwise 500).
- `AddExceptionHandlingForMinimalApi` registers what `AddExceptionHandlingForWebApi` does; call one of them.

## Documentation

https://shared.tailoredapps.pl/en/Libraries/ExceptionHandling/MinimalApi/
