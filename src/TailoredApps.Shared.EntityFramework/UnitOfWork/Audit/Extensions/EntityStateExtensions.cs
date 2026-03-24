using Microsoft.EntityFrameworkCore;
using System;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions
{
    /// <summary>
    /// Provides extension methods for converting between EF Core's <see cref="EntityState"/>
    /// and the audit-specific <see cref="AuditEntityState"/> enum.
    /// </summary>
    public static class EntityStateExtensions
    {
        /// <summary>
        /// Converts an EF Core <see cref="EntityState"/> to the corresponding <see cref="AuditEntityState"/>.
        /// </summary>
        /// <param name="enumValue">The EF Core entity state to convert.</param>
        /// <returns>The matching <see cref="AuditEntityState"/> value.</returns>
        /// <exception cref="InvalidCastException">
        /// Thrown when the EF Core state has no matching value in <see cref="AuditEntityState"/>.
        /// </exception>
        public static AuditEntityState ToAuditEntityState(this EntityState enumValue)
        {
            if (!Enum.TryParse(enumValue.ToString(), true, out AuditEntityState resultingEnum))
                throw new InvalidCastException($"Unable to parse 'Microsoft.EntityFrameworkCore.EntityState.{enumValue.ToString()}` as enum '{typeof(AuditEntityState).FullName}'."
                                               + " Make sure that it has been defined in enum definition.");

            return resultingEnum;
        }

        /// <summary>
        /// Converts an <see cref="AuditEntityState"/> back to the corresponding EF Core <see cref="EntityState"/>.
        /// </summary>
        /// <param name="enumValue">The audit entity state to convert.</param>
        /// <returns>The matching EF Core <see cref="EntityState"/> value.</returns>
        /// <exception cref="InvalidCastException">
        /// Thrown when the audit state has no matching value in <see cref="EntityState"/>.
        /// </exception>
        public static EntityState ToEfCoreEntityState(this AuditEntityState enumValue)
        {
            if (!Enum.TryParse(enumValue.ToString(), true, out EntityState resultingEnum))
                throw new InvalidCastException($"Unable to parse 'BC.UnitOfWork.Audit.Abstractions.AuditEntityState.{enumValue.ToString()}` as enum '{typeof(EntityState).FullName}'."
                                               + " Make sure that it has been defined in enum definition.");

            return resultingEnum;
        }
    }
}
