using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Extensions;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit
{
    internal class UnitOfWorkAuditContext : IUnitOfWorkAuditContext
    {
        private readonly IAuditChangesCollector _auditChangesCollector;
        private readonly IEntityChangesAuditor _entityChangesAuditor;
        private readonly List<IAuditEntityEntry> _changedEntitiesBuffer;
        private readonly IDictionary<string, IInternalEntityChange> _changedEntities;

        public UnitOfWorkAuditContext(IEntityChangesAuditor entityChangesAuditor, IAuditChangesCollector auditChangesCollector)
        {
            _entityChangesAuditor = entityChangesAuditor;
            _auditChangesCollector = auditChangesCollector;

            _changedEntities = new Dictionary<string, IInternalEntityChange>();
            _changedEntitiesBuffer = new List<IAuditEntityEntry>();
        }

        public void CollectChanges()
        {
            _changedEntitiesBuffer.Clear();
            _changedEntitiesBuffer.AddRange(_auditChangesCollector.CollectChanges());
        }

        public void PostCollectChanges()
        {
            foreach (var entityEntry in _changedEntitiesBuffer)
                InsertOrUpdate(entityEntry);
            _changedEntitiesBuffer.Clear();
        }

        public void DiscardChanges()
        {
            _changedEntitiesBuffer.Clear();
            _changedEntities.Clear();
        }

        public void AuditChanges()
        {
            if (_changedEntities.Count > 0)
                _entityChangesAuditor.AuditChanges(_changedEntities.Values.Select(e => e as EntityChange));

            DiscardChanges();
        }

        private void InsertOrUpdate(IAuditEntityEntry auditEntityEntry)
        {
            var identifier = auditEntityEntry.GetPrimaryKeyStringIdentifier();
            var collectedEntityChange = auditEntityEntry.CreateInternalEntityChange();

            if (auditEntityEntry.EntityState == AuditEntityState.Added)
                auditEntityEntry.SetPrimaryKeys();

            if (_changedEntities.ContainsKey(identifier))
                Update(identifier, collectedEntityChange);
            else
                _changedEntities.Add(identifier, collectedEntityChange);
        }

        private void Update(string identifier, IInternalEntityChange collectedEntityChange)
        {
            var entityChangeUpdateContext = new EntityChangeUpdateContext(_changedEntities, collectedEntityChange, identifier);
            var stateTransition = new EntityStateTransition(entityChangeUpdateContext.ExistingEntityChange.GetAuditEntityState(), entityChangeUpdateContext.CollectedEntityChange.GetAuditEntityState());
            var updateOperation = EntityChangeUpdateOperationFactory.Create(stateTransition);

            updateOperation(entityChangeUpdateContext);
        }
    }
}