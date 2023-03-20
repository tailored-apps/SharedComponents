using AutoMoqCore;
using Moq;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Utils;
using TailoredApps.Shared.EntityFramework.UnitOfWork;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork.Hooks
{
    public class UnitOfWorkTests
    {
        private AutoMoqer _autoMoqer;
        private UnitOfWork<InMemoryDbContext> _sut;
        private Mock<IHooksManager> _hooksManagerMock;


        public UnitOfWorkTests()
        {
            _autoMoqer = new AutoMoqer();
            _sut = CreateUoW();
            _hooksManagerMock = _autoMoqer.GetMock<IHooksManager>();
        }

        [Fact]
        public void Should_Execute_Pre_And_Post_SaveChanges_Hooks_OnSaveChanges()
        {
            // act
            _sut.SaveChanges();

            // assert
            _hooksManagerMock.Verify(manager => manager.ExecutePostSaveChangesHooks(), Times.Once);
            _hooksManagerMock.Verify(manager => manager.ExecutePreSaveChangesHooks(), Times.Once);
        }

        [Fact]
        public async Task Should_Execute_Pre_And_Post_SaveChanges_Hooks_OnSaveChangesAsync()
        {
            // act
            await _sut.SaveChangesAsync();

            // assert
            _hooksManagerMock.Verify(manager => manager.ExecutePostSaveChangesHooks(), Times.Once);
            _hooksManagerMock.Verify(manager => manager.ExecutePreSaveChangesHooks(), Times.Once);
        }

        [Fact]
        public void Should_Execute_Pre_And_Post_SaveChanges_And_Transaction_Commit_Hooks_OnCommitTransaction()
        {
            // act
            _sut.CommitTransaction();

            // assert
            _hooksManagerMock.Verify(manager => manager.ExecutePostSaveChangesHooks(), Times.Once);
            _hooksManagerMock.Verify(manager => manager.ExecutePreSaveChangesHooks(), Times.Once);
            _hooksManagerMock.Verify(manager => manager.ExecuteTransactionCommitHooks(), Times.Once);
        }

        [Fact]
        public void Should_Execute_Pre_And_Post_SaveChanges_And_Transaction_Commit_Hooks_OnCommitTransaction_With_IsolationLevel()
        {
            // act
            _sut.CommitTransaction(IsolationLevel.Chaos);

            // assert
            _hooksManagerMock.Verify(manager => manager.ExecutePostSaveChangesHooks(), Times.Once);
            _hooksManagerMock.Verify(manager => manager.ExecutePreSaveChangesHooks(), Times.Once);
            _hooksManagerMock.Verify(manager => manager.ExecuteTransactionCommitHooks(), Times.Once);
        }

        [Fact]
        public void Should_Execute_Transaction_Rollback_Hooks_OnRollbackTransaction()
        {
            // act
            _sut.RollbackTransaction();

            // assert
            _hooksManagerMock.Verify(manager => manager.ExecuteTransactionRollbackHooks(), Times.Once);
        }

        [Fact]
        public void Should_Execute_Transaction_Rollback_Hooks_OnRollbackTransaction_With_IsolationLevel()
        {
            // act
            _sut.RollbackTransaction(IsolationLevel.Chaos);

            // assert
            _hooksManagerMock.Verify(manager => manager.ExecuteTransactionRollbackHooks(), Times.Once);
        }

        private UnitOfWork<InMemoryDbContext> CreateUoW()
        {
            var context = _autoMoqer.GetMock<IUnitOfWorkContext>();

            context.Setup(unitOfWorkContext => unitOfWorkContext.GetDbConnection())
                .Returns(_autoMoqer.GetMock<DbConnection>().Object);

            context.Setup(unitOfWorkContext => unitOfWorkContext.BeginTransaction(It.IsAny<IsolationLevel>()))
                .Returns(new Mock<ITransaction>().Object);

            return _autoMoqer.Create<UnitOfWork<InMemoryDbContext>>();
        }
    }
}
