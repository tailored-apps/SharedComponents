using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
    internal interface IAuditEntityEntry
    {
        AuditEntityState EntityState { get; }

        object CurrentEntity { get; }

        object OriginalEntity { get; }

        Type EntityType { get; }

        string GetPrimaryKeyStringIdentifier();
        Dictionary<string, object> GetPrimaryKeys();

        void SetPrimaryKeys();
    }

    internal class AuditEntityEntry : IAuditEntityEntry
    {
        private readonly EntityEntry _entityEntry;

        public AuditEntityEntry(EntityEntry entityEntry)
        {
            _entityEntry = entityEntry ?? throw new ArgumentNullException(nameof(entityEntry));
            CurrentEntity = _entityEntry.CurrentValues.ToObject();
            OriginalEntity = _entityEntry.OriginalValues.ToObject();
            EntityType = entityEntry.Metadata.ClrType;
            EntityState = entityEntry.State.ToAuditEntityState();
        }

        public static IAuditEntityEntry Create(EntityEntry entityEntry)
            => new AuditEntityEntry(entityEntry);

        public AuditEntityState EntityState { get; }

        public object CurrentEntity { get; }

        public object OriginalEntity { get; }

        public Type EntityType { get; }

        public string GetPrimaryKeyStringIdentifier()
        {
            var primaryKeyValues = _entityEntry.Metadata.FindPrimaryKey()
                .Properties
                .Select(key => key.PropertyInfo.GetValue(_entityEntry.Entity));

            return $"{EntityType.Name}_{string.Join("_", primaryKeyValues)}";
        }
        public Dictionary<string, object> GetPrimaryKeys()
        {
            var primaryKey = _entityEntry.Metadata.FindPrimaryKey();

            var keys = primaryKey.Properties.ToDictionary(x => x.Name, x => x.PropertyInfo.GetValue(_entityEntry.Entity));

            return keys;
        }

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