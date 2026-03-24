using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Shared.EntityFramework.Extensions
{
    /// <summary>
    /// Provides extension methods for <see cref="EntityTypeBuilder{T}"/> to simplify
    /// mapping of shared entity interfaces.
    /// </summary>
    public static class EntityTypeBuilderExtension
    {
        /// <summary>
        /// Configures the standard activity-tracking columns (<c>CreatedBy</c>, <c>CreatedDateUtc</c>,
        /// <c>ModifiedBy</c>, <c>ModifiedDateUtc</c>) for entities that implement <see cref="IActivity"/>.
        /// </summary>
        /// <typeparam name="T">The entity type that implements <see cref="IActivity"/>.</typeparam>
        /// <param name="entity">The entity type builder to configure.</param>
        public static void AddIActivity<T>(this EntityTypeBuilder<T> entity) where T : class, IActivity
        {
            entity.Property(t => t.CreatedBy).HasColumnName("CreatedBy").IsRequired().HasMaxLength(321);
            entity.Property(t => t.CreatedDateUtc).HasColumnName("CreatedDateUtc").HasDefaultValue(DateTime.UtcNow).IsRequired();
            entity.Property(t => t.ModifiedBy).HasColumnName("ModifiedBy").HasMaxLength(321);
            entity.Property(t => t.ModifiedDateUtc).HasColumnName("ModifiedDateUtc");
        }
    }
}
