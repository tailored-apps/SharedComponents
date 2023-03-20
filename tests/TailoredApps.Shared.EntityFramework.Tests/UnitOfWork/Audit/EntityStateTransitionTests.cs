using System;
using System.Collections.Generic;
using System.Linq;
using TailoredApps.Shared.EntityFramework.Interfaces.Audit;
using TailoredApps.Shared.EntityFramework.UnitOfWork.Audit.Changes;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Audit
{
    public class EntityStateTransitionTests
    {
        private static readonly Random Random = new Random();
        private IDictionary<EntityStateTransition, int> _testDictionary;

        public EntityStateTransitionTests()
        {
            var entityStates = Enum.GetValues(typeof(AuditEntityState)).Cast<AuditEntityState>().ToList();
            _testDictionary = entityStates.SelectMany(x => entityStates, (from, to) => new EntityStateTransition(from, to)).ToDictionary(transition => transition, transition => Random.Next(0, 100));
        }

        [Theory]
        [MemberData(nameof(EntityStateTransitionDictionaryKeys))]
        public void Should_Work_As_Key_For_All_Combinations(AuditEntityState from, AuditEntityState to)
        {
            var stateTransition = new EntityStateTransition(from, to);

            // assert
            Assert.True(_testDictionary.ContainsKey(stateTransition));
        }

        public static IEnumerable<object[]> EntityStateTransitionDictionaryKeys()
        {
            var entityStates = Enum.GetValues(typeof(AuditEntityState)).Cast<AuditEntityState>().ToList();
            var resp = entityStates.SelectMany(x => entityStates, (from, to) => new EntityStateTransition(from, to)).Select(key => new object[] { key.From, key.To }).ToArray();
            return resp;
        }

    }
}
