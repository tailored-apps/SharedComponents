using TailoredApps.Demo.DataModel.Catalog.Model;

namespace TailoredApps.Demo.Client.Services;

/// <summary>What the customer has picked so far. It lives in the browser until it becomes an order.</summary>
public sealed class Basket
{
    private readonly List<BasketLine> lines = [];

    public event Action? Changed;

    public IReadOnlyList<BasketLine> Lines => lines;

    public int Count => lines.Sum(line => line.Quantity);

    public decimal Total => lines.Sum(line => line.Product.Price * line.Quantity);

    /// <summary>The order message takes product ids; an id given twice is two pieces.</summary>
    public int[] ProductIds => lines.SelectMany(line => Enumerable.Repeat(line.Product.Id, line.Quantity)).ToArray();

    public void Add(Product product)
    {
        var line = lines.Find(existing => existing.Product.Id == product.Id);
        if (line is null)
            lines.Add(new BasketLine(product) { Quantity = 1 });
        else
            line.Quantity++;

        Changed?.Invoke();
    }

    public void Remove(BasketLine line)
    {
        lines.Remove(line);
        Changed?.Invoke();
    }

    public void Clear()
    {
        lines.Clear();
        Changed?.Invoke();
    }
}

public sealed class BasketLine(Product product)
{
    public Product Product { get; } = product;

    public int Quantity { get; set; }
}
