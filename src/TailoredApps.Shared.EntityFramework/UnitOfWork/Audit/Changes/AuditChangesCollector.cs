using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{
    internal interface IAuditChangesCollector
    {
        IEnumerable<IAuditEntityEntry> CollectChanges();
    }

    internal class AuditChangesCollector<TDbContext> : IAuditChangesCollector
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IAuditSettings _auditSettings;

        public AuditChangesCollector(TDbContext dbContext, IAuditSettings auditSettings)
        {
            _dbContext = dbContext;
            _auditSettings = auditSettings;
        }

        public IEnumerable<IAuditEntityEntry> CollectChanges()
        {
            _dbContext.ChangeTracker.DetectChanges();

            var efCoreEntityStatesToCollect =
                _auditSettings.EntityStatesToCollect.Select(s => s.ToEfCoreEntityState());

            return _dbContext.ChangeTracker.Entries()
                .Where(entityEntry => _auditSettings.TypesToCollect.Contains(entityEntry.Entity.GetType())
                                      && efCoreEntityStatesToCollect.Contains(entityEntry.State))
                .Select(AuditEntityEntry.Create)
                .ToList();
        }
    }
}