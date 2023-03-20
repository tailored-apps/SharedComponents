using System;
using System.Collections.Generic;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
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

        public static Action<IEntityChangeUpdateContext> Create(EntityStateTransition entityStateTransition)
        {
            if (UpdateOperations.TryGetValue(entityStateTransition, out var updateOperation))
                return updateOperation;
            else
                throw new InvalidOperationException("Unexpected entity state transition within current transaction.");
        }
    }
}