using Microsoft.EntityFrameworkCore;
using TailoredApps.Demo.Services.Catalog.Entities;
using TailoredApps.Demo.Services.Internals;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Demo.Services.Catalog;

public class CatalogEntitiesMapping : IModelBuilder
{
    public void MapModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Product");
            entity.HasKey(product => product.Id);

            entity.Property(product => product.Id).ValueGeneratedOnAdd();
            entity.Property(product => product.Name).HasMaxLength(100).IsRequired();
            entity.Property(product => product.Category).HasMaxLength(50).IsRequired();
            entity.Property(product => product.Price).HasConversion(SqliteConversions.MinorUnits);

            entity.HasIndex(product => product.Name).IsUnique();
        });
    }
}
