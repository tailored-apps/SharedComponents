using System.Diagnostics.Metrics;

namespace TailoredApps.Demo.Services.Internals;

/// <summary>
/// The counters of the service, as a <see cref="Meter"/> (OpenTelemetry can export it) and as plain numbers
/// the tests read. Filled by the mediator's processors and notification handlers, not by the handlers
/// themselves.
/// </summary>
public sealed class ShopMetrics : IDisposable
{
    public const string MeterName = "TailoredApps.Demo";

    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> paymentsStarted;
    private readonly Counter<long> gatewayOutages;
    private readonly Counter<long> ordersPaid;
    private readonly Counter<long> productsExported;
    private int started;
    private int outages;
    private int paid;
    private int exported;

    public ShopMetrics()
    {
        paymentsStarted = meter.CreateCounter<long>("demo.payments.started");
        gatewayOutages = meter.CreateCounter<long>("demo.payments.gateway_outages");
        ordersPaid = meter.CreateCounter<long>("demo.orders.paid");
        productsExported = meter.CreateCounter<long>("demo.catalog.products_exported");
    }

    public int PaymentsStarted => started;

    public int GatewayOutages => outages;

    public int OrdersPaid => paid;

    public int ProductsExported => exported;

    public void PaymentStarted(string provider)
    {
        Interlocked.Increment(ref started);
        paymentsStarted.Add(1, new KeyValuePair<string, object?>("provider", provider));
    }

    public void GatewayOutage(string provider)
    {
        Interlocked.Increment(ref outages);
        gatewayOutages.Add(1, new KeyValuePair<string, object?>("provider", provider));
    }

    public void OrderPaid()
    {
        Interlocked.Increment(ref paid);
        ordersPaid.Add(1);
    }

    public void ProductExported()
    {
        Interlocked.Increment(ref exported);
        productsExported.Add(1);
    }

    public void Dispose() => meter.Dispose();
}
