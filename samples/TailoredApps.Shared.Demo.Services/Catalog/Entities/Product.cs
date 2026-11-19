using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Demo.Services.Catalog.Entities;

public class Product : IModelBase<int>
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }
}
