using TailoredApps.Shared.EntityFramework.Interfaces.Audit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes
{

    internal class EntityStateTransition
    {
        public EntityStateTransition(AuditEntityState from, AuditEntityState to)
        {
            From = from;
            To = to;
        }

        public AuditEntityState From { get; }
        public AuditEntityState To { get; }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + From.GetHashCode();
                hash = hash * 23 + To.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EntityStateTransition otherStateTransition))
                return false;

            return From == otherStateTransition.From && To == otherStateTransition.To;
        }
    }
}