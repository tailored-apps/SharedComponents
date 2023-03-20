using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using TailoredApps.Shared.EntityFramework.Interfaces;

namespace TailoredApps.Shared.EntityFramework.Extensions
{
    public static class EntityTypeBuilderExtension
    {
        public static void AddIActivity<T>(this EntityTypeBuilder<T> entity) where T : class, IActivity
        {
            entity.Property(t => t.CreatedBy).HasColumnName("CreatedBy").IsRequired().HasMaxLength(321);
            entity.Property(t => t.CreatedDateUtc).HasColumnName("CreatedDateUtc").HasDefaultValue(DateTime.UtcNow).IsRequired();
            entity.Property(t => t.ModifiedBy).HasColumnName("ModifiedBy").HasMaxLength(321);
            entity.Property(t => t.ModifiedDateUtc).HasColumnName("ModifiedDateUtc");
        }
    }
}
