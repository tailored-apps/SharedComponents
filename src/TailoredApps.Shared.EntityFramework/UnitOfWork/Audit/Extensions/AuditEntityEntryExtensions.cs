using System;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions
{
    internal static class AuditEntityEntryExtensions
    {
        private static readonly Type InternalEntityChangeGenericType = typeof(InternalEntityChange<>);

        public static IInternalEntityChange CreateInternalEntityChange(this IAuditEntityEntry auditEntityEntry)
        {
            var genericType = InternalEntityChangeGenericType.MakeGenericType(auditEntityEntry.EntityType);
            var keys = auditEntityEntry.GetPrimaryKeys();
            var entityChange = Activator.CreateInstance(genericType,
                auditEntityEntry.CurrentEntity,
                auditEntityEntry.OriginalEntity,
                keys,
                auditEntityEntry.EntityState);

            return entityChange as IInternalEntityChange;

        }
    }
}