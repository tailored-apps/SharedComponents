using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TailoredApps.Shared.Payments.Provider.CashBill;

/// <summary>
/// Concrete implementation of <see cref="ICashbillHttpClient"/>.
/// Sends HTTP requests with form-encoded payloads to the CashBill REST API through <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// A response outside 200-299 is reported with <see cref="PaymentGatewayException"/>: the gateway did not give
/// a usable answer, which says nothing about the payment. Every failure except a cancellation is logged
/// without the query string, which carries the request signature.
/// </remarks>
public class CashbillHttpClient : ICashbillHttpClient
{
    /// <summary>The name of the HTTP client used for CashBill when the provider is registered in dependency injection.</summary>
    public const string HttpClientName = "CashBill";

    // The content type CashBill has always been sent by this library, kept byte for byte.
    private const string FormContentType = "application/x-www-form-urlencoded; charset=UTF8";

    // Used only by the constructor without a factory: one shared, pooled client instead of one per request.
    private static readonly HttpClient SharedClient = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });

    private readonly ILogger logger;
    private readonly IHttpClientFactory? httpClientFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="CashbillHttpClient"/> that sends requests through a client of
    /// its own. Prefer the constructor that takes an <see cref="IHttpClientFactory"/>.
    /// </summary>
    /// <param name="logger">Logger instance for error reporting.</param>
    public CashbillHttpClient(ILogger<CashbillHttpClient> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CashbillHttpClient"/> that sends requests through the HTTP
    /// client named <see cref="HttpClientName"/>, so it follows the handlers, resilience and timeouts the
    /// host configures for its HTTP clients.
    /// </summary>
    /// <param name="logger">Logger instance for error reporting.</param>
    /// <param name="httpClientFactory">Creates the <see cref="HttpClientName"/> client.</param>
    public CashbillHttpClient(ILogger<CashbillHttpClient> logger, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    /// <exception cref="PaymentGatewayException">CashBill answered with a status outside 200-299.</exception>
    public async Task<T> MakeFormRequest<T>(string url, string method, FormUrlEncodedContent? requestFormData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var address = new Uri(url);
            if (method != "GET")
                ArgumentNullException.ThrowIfNull(requestFormData);

            using var response = await SendAsync(address, method, method == "GET" ? null : requestFormData, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(json)
                ?? throw new InvalidOperationException("CashBill returned an empty response.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure(exception, method, url);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="PaymentGatewayException">CashBill answered with a status outside 200-299.</exception>
    public async Task MakeFormRequest(string url, string method, FormUrlEncodedContent requestFormData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var address = new Uri(url);
            using var response = await SendAsync(address, method, method == "GET" ? null : requestFormData, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure(exception, method, url);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Uri address, string method, FormUrlEncodedContent? form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), address);
        if (form is not null)
        {
            // A copy of the caller's form: the request owns and disposes its content.
            request.Content = new StringContent(await form.ReadAsStringAsync(cancellationToken), Encoding.UTF8);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(FormContentType);
        }

        var client = httpClientFactory?.CreateClient(HttpClientName) ?? SharedClient;
        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            try
            {
                await PaymentGatewayException.ThrowIfNotSuccessAsync("CashBill", response, cancellationToken);
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
        finally
        {
            // A client from the factory is cheap and owned by this call; the shared one lives on.
            if (!ReferenceEquals(client, SharedClient))
                client.Dispose();
        }
    }

    private void LogFailure(Exception exception, string method, string url)
    {
        // The query string holds the request signature; log the address without it.
        var address = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.GetLeftPart(UriPartial.Path) : "<invalid address>";
        logger.LogError(exception, "CashBill request {Method} {Address} failed.", method, address);
    }
}
