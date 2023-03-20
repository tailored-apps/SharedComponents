using Moq;
using System.Collections.Generic;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Hooks
{

    public class UnitOfWorkHooksManagerTests
    {
        private readonly Mock<IPostSaveChangesHook> _postSaveChangesHook = new Mock<IPostSaveChangesHook>();
        private readonly Mock<IPreSaveChangesHook> _preSaveChangesHook = new Mock<IPreSaveChangesHook>();
        private readonly Mock<ITransactionRollbackHook> _transactionRollback = new Mock<ITransactionRollbackHook>();
        private readonly Mock<ITransactionCommitHook> _transactionCommitHook = new Mock<ITransactionCommitHook>();

        [Fact]
        public void Should_Execute_Appropriate_Hooks()
        {
            // arrange
            var hooks = new List<IHook>
            {
                _postSaveChangesHook.Object,
                _preSaveChangesHook.Object,
                _transactionRollback.Object,
                _transactionCommitHook.Object
            };
            var sut = new UnitOfWorkHooksManager(hooks);

            // act
            sut.ExecuteTransactionCommitHooks();
            sut.ExecutePostSaveChangesHooks();
            sut.ExecutePreSaveChangesHooks();
            sut.ExecuteTransactionRollbackHooks();

            // assert
            _postSaveChangesHook.Verify(hook => hook.Execute(), Times.Once);
            _preSaveChangesHook.Verify(hook => hook.Execute(), Times.Once);
            _transactionRollback.Verify(hook => hook.Execute(), Times.Once);
            _transactionCommitHook.Verify(hook => hook.Execute(), Times.Once);
        }
    }
}
