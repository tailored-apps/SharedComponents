using System;
using System.Collections.Generic;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
    /// <summary>
    /// Factory that maps an <see cref="EntityStateTransition"/> to the appropriate
    /// entity-change update operation. Each operation defines how an existing audit record
    /// should be mutated when an entity transitions between two <see cref="AuditEntityState"/> values
    /// within the same transaction.
    /// </summary>
    internal static class EntityChangeUpdateOperationFactory
    {
        private static readonly IDictionary<EntityStateTransition, Action<IEntityChangeUpdateContext>> UpdateOperations =
            new Dictionary<EntityStateTransition, Action<IEntityChangeUpdateContext>>
            {
                {
                    new EntityStateTransition(AuditEntityState.Added, AuditEntityState.Deleted),
                    changeUpdateContext =>
                        changeUpdateContext.EntityChangesDictionary.Remove(changeUpdateContext.Identifier)
                },
                {
                    new EntityStateTransition(AuditEntityState.Added, AuditEntityState.Modified),
                    changeUpdateContext =>
                    {
                        changeUpdateContext.ExistingEntityChange.SetOriginalEntity(changeUpdateContext
                            .CollectedEntityChange.GetCurrentEntity());
                        changeUpdateContext.ExistingEntityChange.SetCurrentEntity(changeUpdateContext
                            .CollectedEntityChange.GetCurrentEntity());
                    }
                },
                {
                    new EntityStateTransition(AuditEntityState.Modified, AuditEntityState.Modified),
                    changeUpdateContext =>
                    {
                        changeUpdateContext.ExistingEntityChange.SetCurrentEntity(changeUpdateContext
                            .CollectedEntityChange.GetCurrentEntity());
                    }
                },
                {
                    new EntityStateTransition(AuditEntityState.Modified, AuditEntityState.Deleted),
                    changeUpdateContext =>
                    {
                        changeUpdateContext.ExistingEntityChange.SetOriginalEntity(changeUpdateContext
                            .CollectedEntityChange.GetOriginalEntity());
                        changeUpdateContext.ExistingEntityChange.SetCurrentEntity(changeUpdateContext
                            .CollectedEntityChange.GetCurrentEntity());
                        changeUpdateContext.ExistingEntityChange.SetEntityState(AuditEntityState.Deleted);
                    }
                },
                {
                    new EntityStateTransition(AuditEntityState.Deleted, AuditEntityState.Added),
                    changeUpdateContext =>
                    {
                        changeUpdateContext.ExistingEntityChange.SetOriginalEntity(changeUpdateContext
                            .CollectedEntityChange.GetCurrentEntity());
                        changeUpdateContext.ExistingEntityChange.SetCurrentEntity(changeUpdateContext
                            .CollectedEntityChange.GetCurrentEntity());
                    }
                },
            };

        /// <summary>
        /// Returns the update operation delegate for the given <see cref="EntityStateTransition"/>.
        /// </summary>
        /// <param name="entityStateTransition">
        /// The transition describing the previous and new audit state of the entity.
        /// </param>
        /// <returns>
        /// An <see cref="Action{IEntityChangeUpdateContext}"/> that applies the correct merge
        /// strategy to the existing audit change record.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no operation is registered for the supplied <paramref name="entityStateTransition"/>.
        /// </exception>
        public static Action<IEntityChangeUpdateContext> Create(EntityStateTransition entityStateTransition)
        {
            if (UpdateOperations.TryGetValue(entityStateTransition, out var updateOperation))
                return updateOperation;
            else
                throw new InvalidOperationException("Unexpected entity state transition within current transaction.");
        }
    }
}
