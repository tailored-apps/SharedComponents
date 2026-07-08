using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
    /// <summary>
    /// Represents the audit snapshot of a tracked entity entry captured at save time.
    /// Provides access to the entity's current/original state, type, primary keys,
    /// and the audit-specific entity state.
    /// </summary>
    internal interface IAuditEntityEntry
    {
        /// <summary>
        /// Gets the audit-specific state of the entity (e.g. Added, Modified, Deleted).
        /// </summary>
        AuditEntityState EntityState { get; }

        /// <summary>
        /// Gets the entity object with its current (post-change) property values.
        /// </summary>
        object CurrentEntity { get; }

        /// <summary>
        /// Gets the entity object with its original (pre-change) property values.
        /// </summary>
        object OriginalEntity { get; }

        /// <summary>
        /// Gets the CLR type of the tracked entity.
        /// </summary>
        Type EntityType { get; }

        /// <summary>
        /// Returns a string identifier built from the entity type name and its primary key values.
        /// </summary>
        string GetPrimaryKeyStringIdentifier();

        /// <summary>
        /// Returns a dictionary mapping primary key property names to their current values.
        /// </summary>
        Dictionary<string, object> GetPrimaryKeys();

        /// <summary>
        /// Copies primary key values from the EF Core tracked entity into both
        /// <see cref="CurrentEntity"/> and <see cref="OriginalEntity"/> snapshots.
        /// </summary>
        void SetPrimaryKeys();
    }

    /// <summary>
    /// Default implementation of <see cref="IAuditEntityEntry"/> that wraps an EF Core
    /// <see cref="EntityEntry"/> and snapshots its current and original values at construction time.
    /// </summary>
    internal class AuditEntityEntry : IAuditEntityEntry
    {
        private readonly EntityEntry _entityEntry;

        /// <summary>
        /// Initializes a new instance of <see cref="AuditEntityEntry"/> from an EF Core entity entry.
        /// Snapshots <see cref="CurrentEntity"/>, <see cref="OriginalEntity"/>, <see cref="EntityType"/>,
        /// and <see cref="EntityState"/> at the time of construction.
        /// </summary>
        /// <param name="entityEntry">The EF Core change-tracker entry to wrap. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entityEntry"/> is <c>null</c>.</exception>
        public AuditEntityEntry(EntityEntry entityEntry)
        {
            _entityEntry = entityEntry ?? throw new ArgumentNullException(nameof(entityEntry));
            CurrentEntity = _entityEntry.CurrentValues.ToObject();
            OriginalEntity = _entityEntry.OriginalValues.ToObject();
            EntityType = entityEntry.Metadata.ClrType;
            EntityState = entityEntry.State.ToAuditEntityState();
        }

        /// <summary>
        /// Creates a new <see cref="IAuditEntityEntry"/> from the given EF Core entity entry.
        /// </summary>
        /// <param name="entityEntry">The EF Core change-tracker entry to wrap.</param>
        /// <returns>A new <see cref="IAuditEntityEntry"/> instance.</returns>
        public static IAuditEntityEntry Create(EntityEntry entityEntry)
            => new AuditEntityEntry(entityEntry);

        /// <inheritdoc/>
        public AuditEntityState EntityState { get; }

        /// <inheritdoc/>
        public object CurrentEntity { get; }

        /// <inheritdoc/>
        public object OriginalEntity { get; }

        /// <inheritdoc/>
        public Type EntityType { get; }

        /// <inheritdoc/>
        public string GetPrimaryKeyStringIdentifier()
        {
            var primaryKeyValues = _entityEntry.Metadata.FindPrimaryKey()
                .Properties
                .Select(key => key.PropertyInfo.GetValue(_entityEntry.Entity));

            return $"{EntityType.Name}_{string.Join("_", primaryKeyValues)}";
        }

        /// <inheritdoc/>
        public Dictionary<string, object> GetPrimaryKeys()
        {
            var primaryKey = _entityEntry.Metadata.FindPrimaryKey();

            var keys = primaryKey.Properties.ToDictionary(x => x.Name, x => x.PropertyInfo.GetValue(_entityEntry.Entity));

            return keys;
        }

        /// <inheritdoc/>
        public void SetPrimaryKeys()
        {
            var primaryKeyProperties = _entityEntry.Metadata.FindPrimaryKey().Properties;

            foreach (var property in primaryKeyProperties)
            {
                var primaryKeyValue = property.PropertyInfo.GetValue(_entityEntry.Entity);

                property.PropertyInfo.SetValue(CurrentEntity, primaryKeyValue);
                property.PropertyInfo.SetValue(OriginalEntity, primaryKeyValue);
            }
        }
    }
}
