using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TailoredApps.Demo.DataModel.Catalog.Messages.Commands;
using TailoredApps.Demo.DataModel.Catalog.Model;
using TailoredApps.Demo.DataModel.Catalog.Responses.Commands;
using TailoredApps.Demo.DataModel.Catalog.Responses.Queries;
using TailoredApps.Demo.DataModel.Orders.Messages.Commands;
using TailoredApps.Demo.DataModel.Orders.Model;
using TailoredApps.Demo.DataModel.Orders.Responses.Commands;
using TailoredApps.Demo.DataModel.Orders.Responses.Queries;
using TailoredApps.Shared.Payments;

namespace TailoredApps.Demo.Client.Services;

/// <summary>
/// The Web API, as the client sees it. Every request body and every response is a type of the DataModel
/// project - the same class the controller receives and the handler returns.
/// </summary>
public sealed class ShopApi(HttpClient http, Session session)
{
    public Task<GetProductsResponse> GetProductsAsync(int page, int count, string sortField, bool descending, string? category, string? nameContains, CancellationToken cancellationToken)
    {
        var query = $"api/products?page={page}&count={count}&sortField={Uri.EscapeDataString(sortField)}&sortDir={(descending ? "Desc" : "Asc")}";
        if (!string.IsNullOrWhiteSpace(category))
            query += $"&filter.category={Uri.EscapeDataString(category)}";
        if (!string.IsNullOrWhiteSpace(nameContains))
            query += $"&filter.nameContains={Uri.EscapeDataString(nameContains)}";

        return SendAsync<GetProductsResponse>(HttpMethod.Get, query, null, cancellationToken);
    }

    public async Task<Product> GetProductAsync(int id, CancellationToken cancellationToken = default)
        => (await SendAsync<GetProductResponse>(HttpMethod.Get, $"api/products/{id}", null, cancellationToken)).Product;

    public async Task<IReadOnlyList<string>> GetRecommendationsAsync(int id, CancellationToken cancellationToken = default)
        => (await SendAsync<GetRecommendationsResponse>(HttpMethod.Get, $"api/products/{id}/recommendations", null, cancellationToken)).Recommendations;

    public async Task<Product> AddProductAsync(AddProduct product, CancellationToken cancellationToken = default)
        => (await SendAsync<AddProductResponse>(HttpMethod.Post, "api/products", product, cancellationToken)).Product;

    public async Task<Order> CreateOrderAsync(CreateOrder order, CancellationToken cancellationToken = default)
        => (await SendAsync<CreateOrderResponse>(HttpMethod.Post, "api/orders", order, cancellationToken)).Order;

    public async Task<Order> GetOrderAsync(string number, CancellationToken cancellationToken = default)
        => (await SendAsync<GetOrderResponse>(HttpMethod.Get, $"api/orders/{Uri.EscapeDataString(number)}", null, cancellationToken)).Order;

    public Task<CheckoutOrderResponse> CheckoutAsync(string number, string provider, string? channel, CancellationToken cancellationToken = default)
        => SendAsync<CheckoutOrderResponse>(HttpMethod.Post, $"api/orders/{Uri.EscapeDataString(number)}/checkout", new CheckoutOrder { Provider = provider, Channel = channel }, cancellationToken);

    public Task CancelOrderAsync(string number, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"api/orders/{Uri.EscapeDataString(number)}", null, cancellationToken);

    public async Task<IReadOnlyList<string>> GetPaymentProvidersAsync(CancellationToken cancellationToken = default)
        => (await SendAsync<List<PaymentProvider>>(HttpMethod.Get, "api/payments/providers", null, cancellationToken)).Select(provider => provider.Id).ToList();

    /// <summary>
    /// Plays the gateway: asks the service to deliver the "payment completed" notification Revolut would send.
    /// The endpoint exists in the Development environment only.
    /// </summary>
    public Task SimulateGatewayConfirmationAsync(string number, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"api/dev/gateway/revolut/orders/{Uri.EscapeDataString(number)}/complete", null, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string address, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, address, body, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new ApiException(response.StatusCode, new ApiError { Message = "The service sent an empty answer." });
    }

    private async Task SendAsync(HttpMethod method, string address, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, address, body, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string address, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, address);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());
        if (session.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            throw await ApiException.FromAsync(response, cancellationToken);
        }
    }

}

/// <summary>The error body of the service (ExceptionHandling): a message, the status, and one entry per invalid field.</summary>
public sealed class ApiError
{
    public string Message { get; set; } = string.Empty;

    public int ErrorCode { get; set; }

    public List<ApiFieldError> Errors { get; set; } = [];
}

public sealed class ApiFieldError
{
    public string? Field { get; set; }

    public string Message { get; set; } = string.Empty;
}

public sealed class ApiException(HttpStatusCode status, ApiError error) : Exception(error.Message)
{
    public HttpStatusCode Status { get; } = status;

    public ApiError Error { get; } = error;

    /// <summary>What to show a person: the field messages when there are any, the message otherwise.</summary>
    public string Summary => Error.Errors.Count > 0
        ? string.Join(" ", Error.Errors.Select(error => error.Message).Distinct())
        : Error.Message;

    public static async Task<ApiException> FromAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ApiError? error = null;
        try
        {
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            // Not the error model: the status below is all there is to say.
        }

        if (string.IsNullOrWhiteSpace(error?.Message))
        {
            error = new ApiError
            {
                ErrorCode = (int)response.StatusCode,
                Message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Sign in to do this.",
                    HttpStatusCode.Forbidden => "Your account may not do this.",
                    _ => $"The service answered {(int)response.StatusCode}.",
                },
            };
        }

        return new ApiException(response.StatusCode, error);
    }
}
