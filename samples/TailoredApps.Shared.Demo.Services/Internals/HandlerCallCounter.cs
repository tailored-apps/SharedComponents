namespace TailoredApps.Demo.Services.Internals;

/// <summary>Counts handler runs, so that the demo (and its tests) can show a cached response skipping the handler.</summary>
public class HandlerCallCounter
{
    private int getProduct;

    public int GetProduct => getProduct;

    public void CountGetProduct() => Interlocked.Increment(ref getProduct);
}
