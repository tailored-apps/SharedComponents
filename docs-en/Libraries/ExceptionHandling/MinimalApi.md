# TailoredApps.Shared.ExceptionHandling.MinimalApi

[![NuGet](https://img.shields.io/nuget/v/TailoredApps.Shared.ExceptionHandling.MinimalApi)](https://www.nuget.org/packages/TailoredApps.Shared.ExceptionHandling.MinimalApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/tailored-apps/SharedComponents/blob/master/LICENSE)

---

## Description

Exception handling and request validation for **minimal APIs** as endpoint filters, on top of
[ExceptionHandling](index.md): the same provider and the same JSON error model as the middleware and the MVC filter. The library is a
package of its own; it changes nothing in the existing ones.

- **`WithValidation<TRequest>()`** - before the endpoint is called, runs every registered FluentValidation validator for the argument of
  type `TRequest`. A failure is the provider's answer (400 with one entry per field by default) and the endpoint is not called.
- **`WithExceptionHandling()`** - turns an exception of the endpoint (or of a whole group) into the provider's answer. Useful when only a
  part of the application should answer this way; for the whole application the middleware `app.ConfigureExceptionHandler()` is enough.
- **`ToResult()`** - turns an `ExceptionHandlingResultModel` into a minimal-API result with the model's status.

!!! success "NativeAOT"
    The library is safe for trimming and NativeAOT: the request type is a type parameter, so the filter uses no reflection. Checked
    natively in CI with a real FluentValidation validator
    (see [Trimming and NativeAOT](../../Platform/index.md#trimming-and-nativeaot)).

---

## Installation

```bash
dotnet add package TailoredApps.Shared.ExceptionHandling.MinimalApi
```

---

## DI registration

```csharp
// Program.cs
using FluentValidation;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.MinimalApi;
using TailoredApps.Shared.ExceptionHandling.Providers;

builder.Services.AddExceptionHandlingForMinimalApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
builder.Services.AddTransient<IValidator<NewProduct>, NewProductValidator>();
```

`AddExceptionHandlingForMinimalApi` registers what `AddExceptionHandlingForWebApi` does. An application that already has that
registration for its controllers does not repeat it.

---

## Usage example

```csharp
using TailoredApps.Shared.ExceptionHandling.MinimalApi;

public record NewProduct(string Name, decimal Price);

public class NewProductValidator : AbstractValidator<NewProduct>
{
    public NewProductValidator()
    {
        RuleFor(p => p.Name).NotEmpty();
        RuleFor(p => p.Price).GreaterThan(0);
    }
}

var products = app.MapGroup("/api/products").WithExceptionHandling();

// An invalid body -> 400 with the Name and Price fields; the endpoint is not called
products.MapPost("/", (NewProduct product) => Results.Created($"/api/products/{product.Name}", product))
        .WithValidation<NewProduct>();

// An exception -> the provider's answer (404 for KeyNotFoundException in a provider of your own, for example)
products.MapGet("/{id:int}", (int id) => Find(id) ?? throw new KeyNotFoundException($"Product {id} does not exist."));
```

The answer to a validation failure (the same as from the middleware):

```json
{
  "message": "Validation failed: ...",
  "errorCode": 400,
  "errors": [
    { "field": "Name", "message": "'Name' must not be empty." },
    { "field": "Price", "message": "'Price' must be greater than '0'." }
  ]
}
```

---

## API Reference

| Type | Kind | Description |
|------|------|-------------|
| `ExceptionHandlingMinimalApiExtensions.AddExceptionHandlingForMinimalApi<TI, T>` | Ext. method | Registers the exception handling services with the given provider |
| `ExceptionHandlingMinimalApiExtensions.WithExceptionHandling<TBuilder>()` | Ext. method | The exception filter for an endpoint or a group |
| `ExceptionHandlingMinimalApiExtensions.WithValidation<TRequest>()` | Ext. method | The validation filter for the `TRequest` argument, for an endpoint or a group |
| `ExceptionHandlingEndpointFilter` | Endpoint filter | An exception → the provider's error model; `OperationCanceledException` passes through |
| `ValidationEndpointFilter<TRequest>` | Endpoint filter | The `IValidator<TRequest>` validators from DI; no such argument or no validator → the endpoint runs normally |
| `ExceptionHandlingResults.ToResult()` | Ext. method | `ExceptionHandlingResultModel` → `IResult` (status 400-599, otherwise 500) |

---

## 🔒 Behaviour

- **A client error is not an error in the log.** An exception the provider answers with 4xx is logged by the filter at Debug level
  (category `TailoredApps.Shared.ExceptionHandling`); 5xx as Error.
- **Exception details do not leak**: the provider decides; the default one answers unexpected exceptions with a generic message.
- **Several validators** for one type: the failures of all of them are reported.
- **Filter order** is the order they are added: add `WithExceptionHandling()` before the others, so that it covers their exceptions too.

---

## 🤖 AI Agent Prompt

````markdown
## TailoredApps.Shared.ExceptionHandling.MinimalApi - instructions for an AI agent

You are using exception handling and validation in ASP.NET Core minimal APIs through endpoint filters.

### Registration
```csharp
// Program.cs
builder.Services.AddExceptionHandlingForMinimalApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();
builder.Services.AddTransient<IValidator<NewProduct>, NewProductValidator>();

var group = app.MapGroup("/api/products").WithExceptionHandling();
group.MapPost("/", handler).WithValidation<NewProduct>();
```

### What gets configured
- IExceptionHandlingService + the provider - the same as for the middleware and the MVC filter
- WithValidation<TRequest>(): the IValidator<TRequest> validators from DI before the endpoint; a failure -> the error model (400 by default)
- WithExceptionHandling(): an exception of the endpoint or the group -> the provider's error model
- ToResult(): ExceptionHandlingResultModel -> IResult

### Configuration
- No configuration keys. An IExceptionHandlingProvider of your own maps exceptions to statuses

### Rules
- Name the request type explicitly: WithValidation<NewProduct>() - the filter does not scan arguments with reflection (NativeAOT-safe)
- Register validators in DI as IValidator<T>; without a validator the filter lets the request through
- Do not catch exceptions in an endpoint just to return an error - throw, and the provider chooses the status
- Add WithExceptionHandling() as the first filter; for the whole application app.ConfigureExceptionHandler() is enough
- Do not register exception handling twice: AddExceptionHandlingForWebApi and AddExceptionHandlingForMinimalApi are the same registration
- In a NativeAOT application add the request types to your own JsonSerializerContext (a minimal-API requirement, not this library's)
````
