using System;
using System.Collections.Generic;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
    internal interface IInternalEntityChange
    {
        void SetCurrentEntity(object currentEntity);
        void SetOriginalEntity(object originalEntity);
        void SetEntityState(AuditEntityState entityState);
        object GetCurrentEntity();
        object GetOriginalEntity();
        AuditEntityState GetAuditEntityState();
    }

    internal class InternalEntityChange<TEntity> : EntityChange<TEntity>, IInternalEntityChange
        where TEntity : class
    {
        public InternalEntityChange(TEntity currentEntity, TEntity originalEntity, Dictionary<string, object> keys, AuditEntityState state) : base(currentEntity, originalEntity, keys, state)
        {
        }

        public void SetCurrentEntity(object currentEntity)
            => CurrentEntity = currentEntity as TEntity ?? throw new ArgumentException($"Type mismatch. Expected: {typeof(TEntity)}.");

        public void SetOriginalEntity(object originalEntity)
            => OriginalEntity = originalEntity as TEntity ?? throw new ArgumentException($"Type mismatch. Expected: {typeof(TEntity)}.");

        public void SetEntityState(AuditEntityState entityState)
            => State = entityState;

        public object GetCurrentEntity()
            => CurrentEntity;

        public object GetOriginalEntity()
            => OriginalEntity;

        public AuditEntityState GetAuditEntityState()
            => State;
    }
}