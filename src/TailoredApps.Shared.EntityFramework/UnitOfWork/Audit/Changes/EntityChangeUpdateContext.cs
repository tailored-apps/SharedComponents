using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
    internal class EntityChangeUpdateContext : IEntityChangeUpdateContext
    {
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

        public IDictionary<string, IInternalEntityChange> EntityChangesDictionary { get; }
        public string Identifier { get; }
        public IInternalEntityChange CollectedEntityChange { get; }
        public IInternalEntityChange ExistingEntityChange { get; }
    }

    internal interface IEntityChangeUpdateContext
    {
        IDictionary<string, IInternalEntityChange> EntityChangesDictionary { get; }
        string Identifier { get; }
        IInternalEntityChange CollectedEntityChange { get; }
        IInternalEntityChange ExistingEntityChange { get; }
    }
}