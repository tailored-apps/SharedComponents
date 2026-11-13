using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TailoredApps.AotSmoke;
using TailoredApps.Demo.Payments;
using TailoredApps.Shared.DateTime;
using TailoredApps.Shared.Email.Models;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.Adyen;
using TailoredApps.Shared.Payments.Provider.CashBill;
using TailoredApps.Shared.Payments.Provider.HotPay;
using TailoredApps.Shared.Payments.Provider.PayNow;
using TailoredApps.Shared.Payments.Provider.PayU;
using TailoredApps.Shared.Payments.Provider.Przelewy24;
using TailoredApps.Shared.Payments.Provider.Revolut;
using TailoredApps.Shared.Payments.Provider.Tpay;
using TailoredApps.Shared.Querying;
using PaymentRequest = TailoredApps.Shared.Payments.PaymentRequest;

// Runs the AOT-compatible libraries the way an application uses them and reports every check. Published
// with NativeAOT, this is the proof that nothing they need was trimmed away or requires code generated at
// run time. See the project file for how to publish and run it.

var failures = 0;

void Check(string name, bool passed, string? detail = null)
{
    Console.WriteLine($"{(passed ? "ok  " : "FAIL")} {name}{(passed || detail is null ? string.Empty : $" - {detail}")}");
    if (!passed)
        failures++;
}

async Task Run(string name, Func<Task<(bool Passed, string? Detail)>> check)
{
    try
    {
        var (passed, detail) = await check();
        Check(name, passed, detail);
    }
    catch (Exception exception)
    {
        Check(name, false, $"{exception.GetType().Name}: {exception.Message}");
    }
}

const string gateway = FakeGatewayHandler.BaseUrl;
const string webhookSecret = "smoke-not-a-real-secret";

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Payments:Providers:Adyen:ApiKey"] = "key",
        ["Payments:Providers:Adyen:MerchantAccount"] = "Smoke",
        ["Payments:Providers:Adyen:NotificationHmacKey"] = "00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF",
        ["Payments:Providers:Adyen:ReturnUrl"] = "https://localhost/return",
        ["Payments:Providers:Adyen:CheckoutUrl"] = $"{gateway}/adyen/v71",
        ["Payments:Providers:Cashbill:ServiceUrl"] = $"{gateway}/cashbill/",
        ["Payments:Providers:Cashbill:ShopId"] = "smoke",
        ["Payments:Providers:Cashbill:ShopSecretPhrase"] = "secret",
        ["Payments:Providers:Cashbill:ReturnUrl"] = "https://localhost/return",
        ["Payments:Providers:Cashbill:NegativeReturnUrl"] = "https://localhost/failed",
        ["Payments:Providers:HotPay:SecretHash"] = "secret",
        ["Payments:Providers:HotPay:ServiceUrl"] = $"{gateway}/hotpay",
        ["Payments:Providers:HotPay:ReturnUrl"] = "https://localhost/return",
        ["Payments:Providers:PayNow:ApiKey"] = "key",
        ["Payments:Providers:PayNow:SignatureKey"] = "secret",
        ["Payments:Providers:PayNow:ServiceUrl"] = $"{gateway}/paynow",
        ["Payments:Providers:PayU:ClientId"] = "client",
        ["Payments:Providers:PayU:ClientSecret"] = "secret",
        ["Payments:Providers:PayU:PosId"] = "pos",
        ["Payments:Providers:PayU:SignatureKey"] = "secret",
        ["Payments:Providers:PayU:ServiceUrl"] = $"{gateway}/payu",
        ["Payments:Providers:PayU:NotifyUrl"] = "https://localhost/notify",
        ["Payments:Providers:Przelewy24:MerchantId"] = "100001",
        ["Payments:Providers:Przelewy24:PosId"] = "100001",
        ["Payments:Providers:Przelewy24:ApiKey"] = "key",
        ["Payments:Providers:Przelewy24:CrcKey"] = "klucz/żółć",
        ["Payments:Providers:Przelewy24:ServiceUrl"] = $"{gateway}/p24",
        ["Payments:Providers:Przelewy24:ReturnUrl"] = "https://localhost/return",
        ["Payments:Providers:Przelewy24:NotifyUrl"] = "https://localhost/notify",
        ["Payments:Providers:Revolut:ApiKey"] = "key",
        ["Payments:Providers:Revolut:WebhookSecret"] = webhookSecret,
        ["Payments:Providers:Revolut:ApiUrl"] = $"{gateway}/revolut",
        ["Payments:Providers:Tpay:ClientId"] = "client",
        ["Payments:Providers:Tpay:ClientSecret"] = "secret",
        ["Payments:Providers:Tpay:SecurityCode"] = "secret",
        ["Payments:Providers:Tpay:ServiceUrl"] = $"{gateway}/tpay",
        ["Payments:Providers:Tpay:NotifyUrl"] = "https://localhost/notify",
    })
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging();
services.AddDateTimeProvider();
services.AddPayments()
    .RegisterAdyenProvider()
    .RegisterCashbillProvider()
    .RegisterHotPayProvider()
    .RegisterPayNowProvider()
    .RegisterPayUProvider()
    .RegisterPrzelewy24Provider()
    .RegisterRevolutProvider()
    .RegisterTpayProvider();

services.AddSingleton<FakeGatewayState>();
foreach (var client in FakeGatewayHandler.ClientNames)
{
    services.AddHttpClient(client)
        .ConfigurePrimaryHttpMessageHandler(provider => new FakeGatewayHandler(provider.GetRequiredService<FakeGatewayState>()));
}

await using var container = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
using var scope = container.CreateScope();
var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();
var gateways = container.GetRequiredService<FakeGatewayState>();

// Options bound from configuration by the generated binder.
Check("options: Przelewy24 numbers and text are bound",
    container.GetRequiredService<IOptions<Przelewy24ServiceOptions>>().Value is { MerchantId: 100001, PosId: 100001, CrcKey: "klucz/żółć" });
Check("options: Revolut address is bound",
    container.GetRequiredService<IOptions<RevolutServiceOptions>>().Value.ApiUrl == $"{gateway}/revolut");

await Run("providers: all eight are registered", async () =>
{
    var keys = (await payments.GetProviders()).Select(p => p.Id).Order(StringComparer.Ordinal).ToArray();
    string[] expected = ["Adyen", "Cashbill", "HotPay", "PayNow", "PayU", "Przelewy24", "Revolut", "Tpay"];
    return (keys.SequenceEqual(expected), string.Join(",", keys));
});

// A payment at every gateway: request serialized, answer deserialized, both with generated JSON metadata.
foreach (var provider in new[] { "Adyen", "Cashbill", "HotPay", "PayNow", "PayU", "Przelewy24", "Revolut", "Tpay" })
{
    await Run($"payment: {provider} returns a payment page", async () =>
    {
        var payment = await payments.RegisterPayment(new PaymentRequest(provider, 153.90m, "PLN")
        {
            PaymentChannel = provider == "Cashbill" ? "blik" : null,
            Title = "Zamówienie 42",
            Email = "anna@demo.example",
            AdditionalData = "ORD-42",
        });
        var ok = payment.PaymentStatus != PaymentStatusEnum.Rejected && Uri.IsWellFormedUriString(payment.RedirectUrl, UriKind.Absolute);
        return (ok, $"{payment.PaymentStatus} {payment.RedirectUrl} {payment.ResponseObject}");
    });
}

await Run("channels: CashBill list is read from the gateway", async () =>
{
    var channels = await payments.GetChannels("Cashbill", "PLN");
    return (channels.Any(c => c.Id == "blik"), string.Join(",", channels.Select(c => c.Id)));
});

await Run("status: Revolut reports a completed order", async () =>
{
    var status = await payments.GetStatus("Revolut", "REVOLUT-1");
    return (status.PaymentStatus == PaymentStatusEnum.Finished, status.PaymentStatus.ToString());
});

await Run("outage: a failing gateway is PaymentGatewayException", async () =>
{
    gateways.Unavailable = true;
    try
    {
        await payments.RegisterPayment(new PaymentRequest("Revolut", 10m, "PLN"));
        return (false, "no exception");
    }
    catch (PaymentGatewayException exception)
    {
        return (exception.IsTransient && exception.Provider == "Revolut", exception.Message);
    }
    finally
    {
        gateways.Unavailable = false;
    }
});

await Run("refusal: a 4xx answer is Rejected", async () =>
{
    gateways.Refusing = true;
    try
    {
        var payment = await payments.RegisterPayment(new PaymentRequest("PayNow", 10m, "PLN"));
        return (payment.PaymentStatus == PaymentStatusEnum.Rejected, payment.PaymentStatus.ToString());
    }
    finally
    {
        gateways.Refusing = false;
    }
});

await Run("webhook: a signed Revolut notification is processed", async () =>
{
    const string body = """{"event":"ORDER_COMPLETED","order_id":"REVOLUT-1"}""";
    var timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    var signature = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), Encoding.UTF8.GetBytes($"v1.{timestamp}.{body}")));

    var result = await payments.HandleWebhookAsync("Revolut", new PaymentWebhookRequest
    {
        Body = body,
        Headers = new() { ["Revolut-Request-Timestamp"] = timestamp, ["Revolut-Signature"] = $"v1={signature}" },
    });
    return (result is PaymentWebhookProcessed { Payment.PaymentStatus: PaymentStatusEnum.Finished }, result.GetType().Name);
});

await Run("webhook: a forged Revolut notification is rejected", async () =>
{
    var result = await payments.HandleWebhookAsync("Revolut", new PaymentWebhookRequest
    {
        Body = "{}",
        Headers = new() { ["Revolut-Request-Timestamp"] = "1", ["Revolut-Signature"] = "v1=00" },
    });
    return (result is PaymentWebhookRejected, result.GetType().Name);
});

// Przelewy24 signs JSON with unescaped national characters and slashes; the expected text is spelled out.
{
    const string expectedJson = "{\"sessionId\":\"zamówienie/42+ą\",\"merchantId\":100001,\"amount\":999,\"currency\":\"PLN\",\"crc\":\"klucz/żółć\"}";
    var expected = Convert.ToHexStringLower(SHA384.HashData(Encoding.UTF8.GetBytes(expectedJson)));
    var sign = scope.ServiceProvider.GetRequiredService<IPrzelewy24ServiceCaller>().ComputeSign("zamówienie/42+ą", 100001, 999, "PLN");
    Check("signature: Przelewy24 hashes the unescaped JSON", sign == expected, sign);
}

// The retired configuration key stops the application instead of sending a sandbox setup to production.
try
{
    var retired = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:Providers:Tpay:ApiUrl"] = "https://sandbox.example" })
        .Build();
    new TpayConfigureOptions(retired).Configure(new TpayServiceOptions());
    Check("options: the retired Tpay ApiUrl key is refused", false, "no exception");
}
catch (InvalidOperationException exception)
{
    Check("options: the retired Tpay ApiUrl key is refused", exception.Message.Contains("ServiceUrl", StringComparison.Ordinal), exception.Message);
}

// DateTime: one clock for the application.
{
    var clock = container.GetRequiredService<IDateTimeProvider>();
    var before = TimeProvider.System.GetUtcNow();
    Check("clock: IDateTimeProvider reads the registered TimeProvider", clock.OffsetUtcNow >= before && clock.UtcNow.Kind == DateTimeKind.Utc);
}

// Querying and Email.Models: contracts only, kept by the trimmer when they are used.
{
    var message = new MailMessage { Sender = "shop@demo.example", Topic = "Order 42" };
    Check("contracts: Email.Models and Querying types are usable",
        message.Topic == "Order 42" && Enum.GetName(SortDirection.Desc) == "Desc");
}

// The MediatR libraries on the mediator's reflection-free registration.
await MediatorChecks.RunAsync(Check);

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures == 0 ? 0 : 1;
