using Microsoft.EntityFrameworkCore;

namespace TailoredApps.Shared.EntityFramework.Interfaces
{
    /// <summary>
    /// Defines the contract for a class that contributes EF Core model configuration
    /// to a <see cref="ModelBuilder"/> during the <c>OnModelCreating</c> phase.
    /// </summary>
    public interface IModelBuilder
    {
        /// <summary>
        /// Applies entity mappings, relationships, and constraints to the provided <paramref name="modelBuilder"/>.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder to configure.</param>
        void MapModel(ModelBuilder modelBuilder);
    }
}
