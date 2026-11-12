using System.Net;
using System.Text;

namespace TailoredApps.Demo.Payments;

/// <summary>
/// What the fake gateways remember, shared by every HTTP client of the application. Tests use it to see what
/// was sent and to simulate an outage.
/// </summary>
public class FakeGatewayState
{
    private int counter;

    /// <summary>When set, every gateway answers 503, as during an outage.</summary>
    public bool Unavailable { get; set; }

    /// <summary>When set, every gateway answers 400, as it does to a request it refuses.</summary>
    public bool Refusing { get; set; }

    public List<string> Requests { get; } = [];

    public int Next() => Interlocked.Increment(ref counter);
}

/// <summary>
/// Stands in for the payment gateways, so that the demo runs with no account anywhere: it is the primary
/// handler of the providers' HTTP clients and answers the way each gateway does. The first segment of the
/// request path names the gateway (see the <c>ServiceUrl</c> values in appsettings.json).
/// </summary>
public class FakeGatewayHandler(FakeGatewayState state) : HttpMessageHandler
{
    public const string BaseUrl = "https://gateway.demo";

    /// <summary>
    /// The named HTTP clients the payment providers use. Stripe is the exception: its SDK has its own client,
    /// which Program.cs points at the "Stripe" client created here.
    /// </summary>
    public static readonly string[] ClientNames =
        ["Adyen", "CashBill", "HotPay", "PayNow", "PayU", "PayU.Orders", "Przelewy24", "Revolut", "Stripe", "Tpay"];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        lock (state.Requests)
            state.Requests.Add($"{request.Method} {path}");

        if (state.Unavailable)
            return Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, """{"error":"The gateway is down for maintenance."}"""));

        if (state.Refusing)
            return Task.FromResult(Json(HttpStatusCode.BadRequest, """{"error":{"type":"invalid_request_error","message":"The gateway refused the request."}}"""));

        var gateway = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var post = request.Method == HttpMethod.Post;
        var id = $"{gateway.ToUpperInvariant()}-{state.Next()}";
        var page = $"{BaseUrl}/pay/{gateway}/{id}";

        var response = gateway switch
        {
            "adyen" when post => Json(HttpStatusCode.Created, $$"""{"id":"{{id}}","url":"{{page}}","mode":"hosted"}"""),
            "adyen" => Json(HttpStatusCode.OK, """{"status":"completed"}"""),

            "cashbill" when post => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","redirectUrl":"{{page}}"}"""),
            "cashbill" when path.Contains("paymentchannels") => Json(HttpStatusCode.OK, """[{"id":"blik","name":"BLIK","availableCurrencies":["PLN"]}]"""),
            "cashbill" => Json(HttpStatusCode.OK, """{"id":"demo","status":"PreStart"}"""),

            "hotpay" => Json(HttpStatusCode.OK, $$"""{"STATUS":"SUCCESS","PRZEKIERUJ_DO":"{{page}}","ID_PLATNOSCI":"{{id}}"}"""),

            "paynow" when post => Json(HttpStatusCode.Created, $$"""{"paymentId":"{{id}}","status":"NEW","redirectUrl":"{{page}}"}"""),
            "paynow" => Json(HttpStatusCode.OK, """{"status":"CONFIRMED"}"""),

            "payu" when path.EndsWith("/oauth/authorize", StringComparison.Ordinal) => Json(HttpStatusCode.OK, """{"access_token":"demo-token"}"""),
            "payu" when post => Redirect(page, $$"""{"orderId":"{{id}}","redirectUri":"{{page}}"}"""),
            "payu" => Json(HttpStatusCode.OK, """{"orders":[{"status":"COMPLETED"}]}"""),

            "p24" when post => Json(HttpStatusCode.OK, $$$"""{"data":{"token":"{{{id}}}"}}"""),
            "p24" => Json(HttpStatusCode.OK, "{}"),

            "revolut" when post => Json(HttpStatusCode.Created, $$"""{"id":"{{id}}","checkout_url":"{{page}}","state":"pending"}"""),
            "revolut" => Json(HttpStatusCode.OK, """{"id":"demo","state":"completed"}"""),

            "stripe" when post => Json(HttpStatusCode.OK, $$"""{"id":"cs_test_{{id}}","object":"checkout.session","url":"{{page}}","status":"open","payment_status":"unpaid"}"""),
            "stripe" => Json(HttpStatusCode.OK, """{"id":"cs_test_demo","object":"checkout.session","status":"complete","payment_status":"paid"}"""),

            "tpay" when path.EndsWith("/oauth/auth", StringComparison.Ordinal) => Json(HttpStatusCode.OK, """{"access_token":"demo-token"}"""),
            "tpay" when post => Json(HttpStatusCode.OK, $$"""{"transactionId":"{{id}}","transactionPaymentUrl":"{{page}}"}"""),
            "tpay" => Json(HttpStatusCode.OK, """{"status":"correct"}"""),

            _ => Json(HttpStatusCode.NotFound, """{"error":"Unknown gateway."}"""),
        };

        return Task.FromResult(response);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Redirect(string location, string json)
    {
        var response = Json(HttpStatusCode.Found, json);
        response.Headers.Location = new Uri(location);
        return response;
    }
}
