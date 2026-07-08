using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
    /// <summary>
    /// Carries all data required by an entity-change update operation:
    /// the full changes dictionary, the identifier of the entity being updated,
    /// the newly collected change, and the existing change record that will be mutated.
    /// </summary>
    internal class EntityChangeUpdateContext : IEntityChangeUpdateContext
    {
        /// <summary>
        /// Initializes a new instance of <see cref="EntityChangeUpdateContext"/>.
        /// </summary>
        /// <param name="entityChangesDictionary">
        /// The mutable dictionary that maps entity identifiers to their accumulated change records.
        /// Must not be <c>null</c> and must already contain an entry for <paramref name="identifier"/>.
        /// </param>
        /// <param name="collectedEntityChange">The newly captured change to merge into the existing record.</param>
        /// <param name="identifier">The string key that uniquely identifies the entity within the dictionary.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of the required arguments is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="entityChangesDictionary"/> does not contain <paramref name="identifier"/>.
        /// </exception>
        public EntityChangeUpdateContext(IDictionary<string, IInternalEntityChange> entityChangesDictionary,
            IInternalEntityChange collectedEntityChange, string identifier)
        {
            EntityChangesDictionary = entityChangesDictionary ?? throw new ArgumentNullException(nameof(entityChangesDictionary));
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            CollectedEntityChange = collectedEntityChange ?? throw new ArgumentNullException(nameof(collectedEntityChange));

            if (!entityChangesDictionary.TryGetValue(identifier, out var existingEntityChange))
                throw new InvalidOperationException("Entity changes dictionary does not contain required entity.");

            ExistingEntityChange = existingEntityChange;
        }

        /// <inheritdoc/>
        public IDictionary<string, IInternalEntityChange> EntityChangesDictionary { get; }

        /// <inheritdoc/>
        public string Identifier { get; }

        /// <inheritdoc/>
        public IInternalEntityChange CollectedEntityChange { get; }

        /// <inheritdoc/>
        public IInternalEntityChange ExistingEntityChange { get; }
    }

    /// <summary>
    /// Defines the data contract for an entity-change update context used by
    /// <see cref="EntityChangeUpdateOperationFactory"/> to resolve and execute the correct merge strategy.
    /// </summary>
    internal interface IEntityChangeUpdateContext
    {
        /// <summary>
        /// Gets the dictionary of accumulated entity change records, keyed by entity identifier.
        /// </summary>
        IDictionary<string, IInternalEntityChange> EntityChangesDictionary { get; }

        /// <summary>
        /// Gets the string identifier that uniquely identifies the entity within <see cref="EntityChangesDictionary"/>.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Gets the newly collected entity change that should be merged into <see cref="ExistingEntityChange"/>.
        /// </summary>
        IInternalEntityChange CollectedEntityChange { get; }

        /// <summary>
        /// Gets the existing entity change record stored in <see cref="EntityChangesDictionary"/> that will be updated.
        /// </summary>
        IInternalEntityChange ExistingEntityChange { get; }
    }
}
