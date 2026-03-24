using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    /// <summary>
    /// Abstract base class representing a tracked change to an entity within the audit context.
    /// Captures the entity type, its state, and the primary keys involved in the change.
    /// </summary>
    public abstract class EntityChange
    {
        /// <summary>
        /// Initializes a new instance of <see cref="EntityChange"/> with the specified audit state.
        /// </summary>
        /// <param name="state">The <see cref="AuditEntityState"/> of the changed entity.</param>
        protected EntityChange(AuditEntityState state)
        {
            State = state;
        }

        /// <summary>
        /// Gets the CLR type of the changed entity.
        /// </summary>
        public Type EntityType { get; protected set; }

        /// <summary>
        /// Gets the audit state of the entity (Added, Modified, or Deleted).
        /// </summary>
        public AuditEntityState State { get; protected set; }

        /// <summary>
        /// Gets the original (pre-change) snapshot of the entity as an untyped object.
        /// </summary>
        public abstract object Original { get; }

        /// <summary>
        /// Gets the current (post-change) snapshot of the entity as an untyped object.
        /// </summary>
        public abstract object Current { get; }

        /// <summary>
        /// Gets or sets the dictionary of primary key names and their values for the changed entity.
        /// </summary>
        public Dictionary<string, object> PrimaryKeys { get; set; }
    }

    /// <summary>
    /// Strongly-typed representation of a tracked entity change.
    /// </summary>
    /// <typeparam name="TEntity">The type of the audited entity.</typeparam>
    public class EntityChange<TEntity> : EntityChange
        where TEntity : class
    {
        /// <summary>
        /// Initializes a new instance of <see cref="EntityChange{TEntity}"/>.
        /// </summary>
        /// <param name="currentEntity">The current (post-change) state of the entity.</param>
        /// <param name="originalEntity">The original (pre-change) state of the entity.</param>
        /// <param name="keys">The primary key values of the entity.</param>
        /// <param name="state">The audit state describing the type of change.</param>
        public EntityChange(TEntity currentEntity, TEntity originalEntity, Dictionary<string, object> keys, AuditEntityState state) : base(state)
        {
            CurrentEntity = currentEntity ?? throw new ArgumentNullException(nameof(currentEntity));
            OriginalEntity = originalEntity ?? throw new ArgumentNullException(nameof(originalEntity));
            EntityType = typeof(TEntity);
            PrimaryKeys = keys;
        }

        /// <summary>
        /// Gets the original (pre-change) state of the entity.
        /// </summary>
        public TEntity OriginalEntity { get; protected set; }

        /// <inheritdoc/>
        public override object Original { get => OriginalEntity; }

        /// <summary>
        /// Gets the current (post-change) state of the entity.
        /// </summary>
        public TEntity CurrentEntity { get; protected set; }

        /// <inheritdoc/>
        public override object Current { get => CurrentEntity; }
    }
}
