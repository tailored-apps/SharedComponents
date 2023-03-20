using Microsoft.EntityFrameworkCore;
using System;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions
{
    public static class EntityStateExtensions
    {
        public static AuditEntityState ToAuditEntityState(this EntityState enumValue)
        {
            if (!Enum.TryParse(enumValue.ToString(), true, out AuditEntityState resultingEnum))
                throw new InvalidCastException($"Unable to parse 'Microsoft.EntityFrameworkCore.EntityState.{enumValue.ToString()}` as enum '{typeof(AuditEntityState).FullName}'."
                                               + " Make sure that it has been defined in enum definition.");

            return resultingEnum;
        }

        public static EntityState ToEfCoreEntityState(this AuditEntityState enumValue)
        {
            if (!Enum.TryParse(enumValue.ToString(), true, out EntityState resultingEnum))
                throw new InvalidCastException($"Unable to parse 'BC.UnitOfWork.Audit.Abstractions.AuditEntityState.{enumValue.ToString()}` as enum '{typeof(EntityState).FullName}'."
                                               + " Make sure that it has been defined in enum definition.");

            return resultingEnum;
        }
    }
}