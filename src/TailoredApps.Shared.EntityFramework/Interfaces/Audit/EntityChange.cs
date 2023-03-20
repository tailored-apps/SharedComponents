using System;
using System.Collections.Generic;

namespace TailoredApps.Shared.EntityFramework.Interfaces.Audit
{
    public abstract class EntityChange
    {
        protected EntityChange(AuditEntityState state)
        {
            State = state;
        }

        public Type EntityType { get; protected set; }

        public AuditEntityState State { get; protected set; }
        public abstract object Original { get; }

        public abstract object Current { get; }
        public Dictionary<string, object> PrimaryKeys { get; set; }
    }

    public class EntityChange<TEntity> : EntityChange
        where TEntity : class
    {
        public EntityChange(TEntity currentEntity, TEntity originalEntity, Dictionary<string, object> keys, AuditEntityState state) : base(state)
        {
            CurrentEntity = currentEntity ?? throw new ArgumentNullException(nameof(currentEntity));
            OriginalEntity = originalEntity ?? throw new ArgumentNullException(nameof(originalEntity));
            EntityType = typeof(TEntity);
            PrimaryKeys = keys;
        }

        public TEntity OriginalEntity { get; protected set; }
        public override object Original { get => OriginalEntity; }
        public TEntity CurrentEntity { get; protected set; }
        public override object Current { get => CurrentEntity; }
    }
}