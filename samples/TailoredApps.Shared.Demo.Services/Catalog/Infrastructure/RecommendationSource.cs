namespace TailoredApps.Demo.Services.Catalog.Infrastructure;

/// <summary>Stands in for a remote service. The first calls fail, as many as <see cref="FailuresLeft"/> says.</summary>
public class RecommendationSource
{
    public int FailuresLeft { get; set; }

    public int Calls { get; private set; }

    public IReadOnlyList<string> For(int productId)
    {
        Calls++;
        if (FailuresLeft > 0)
        {
            FailuresLeft--;
            throw new TimeoutException("The recommendation service did not answer.");
        }

        return [$"Customers who bought product {productId} also bought a teapot."];
    }
}
