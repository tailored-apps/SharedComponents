namespace TailoredApps.Shared.EntityFramework.Interfaces
{
    /// <summary>
    /// Strongly-typed base interface for entities that expose an identifier of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the entity's primary key.</typeparam>
    public interface IModelBase<T> : IModelBase
    {
        /// <summary>
        /// Gets or sets the primary key of the entity.
        /// </summary>
        T Id { get; set; }
    }

    /// <summary>
    /// Marker interface for all entity model base types in the EntityFramework shared layer.
    /// Used as a constraint for generic query helpers.
    /// </summary>
    public interface IModelBase
    {
    }
}
