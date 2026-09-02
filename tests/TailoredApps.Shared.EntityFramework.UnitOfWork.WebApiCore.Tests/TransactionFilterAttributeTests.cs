using System;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Filters;
using TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Tests.TestSupport;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Tests
{
    public class TransactionFilterAttributeTests
    {
        private readonly Mock<IUnitOfWork> unitOfWorkMock;
        private readonly TransactionFilterAttribute filter;

        public TransactionFilterAttributeTests()
        {
            unitOfWorkMock = new Mock<IUnitOfWork>();
            filter = new TransactionFilterAttribute(unitOfWorkMock.Object);
        }

        #region OnActionExecuted

        [Fact]
        public void When_Action_Threw_Exception_Should_Rollback_And_Not_Commit()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            context.Exception = new InvalidOperationException("action failed");

            // act
            filter.OnActionExecuted(context);

            // assert
            unitOfWorkMock.Verify(x => x.RollbackTransaction(), Times.Once);
            unitOfWorkMock.Verify(x => x.CommitTransaction(), Times.Never);
        }

        [Fact]
        public void When_Action_Threw_Exception_Should_Leave_Exception_And_Result_Untouched()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            var actionException = new InvalidOperationException("action failed");
            var originalResult = new StatusCodeResult(500);
            context.Exception = actionException;
            context.Result = originalResult;

            // act
            filter.OnActionExecuted(context);

            // assert
            Assert.Same(actionException, context.Exception);
            Assert.Same(originalResult, context.Result);
            Assert.False(context.ExceptionHandled);
        }

        [Fact]
        public void When_Action_Succeeded_Should_Commit_And_Not_Rollback()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();

            // act
            filter.OnActionExecuted(context);

            // assert
            unitOfWorkMock.Verify(x => x.CommitTransaction(), Times.Once);
            unitOfWorkMock.Verify(x => x.RollbackTransaction(), Times.Never);
        }

        [Fact]
        public void When_Action_Succeeded_Should_Not_Set_Exception_Or_Clear_Result()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            var originalResult = new OkResult();
            context.Result = originalResult;

            // act
            filter.OnActionExecuted(context);

            // assert
            Assert.Null(context.Exception);
            Assert.Same(originalResult, context.Result);
        }

        [Fact]
        public void When_Commit_Throws_Should_Rollback_Transaction()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            unitOfWorkMock.Setup(x => x.CommitTransaction()).Throws(new InvalidOperationException("commit failed"));

            // act
            filter.OnActionExecuted(context);

            // assert
            unitOfWorkMock.Verify(x => x.CommitTransaction(), Times.Once);
            unitOfWorkMock.Verify(x => x.RollbackTransaction(), Times.Once);
        }

        [Fact]
        public void When_Commit_Throws_Should_Set_Context_Exception_To_Commit_Exception()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            var commitException = new InvalidOperationException("commit failed");
            unitOfWorkMock.Setup(x => x.CommitTransaction()).Throws(commitException);

            // act
            filter.OnActionExecuted(context);

            // assert
            Assert.Same(commitException, context.Exception);
        }

        [Fact]
        public void When_Commit_Throws_Should_Null_The_Result()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            context.Result = new OkObjectResult("payload produced by the action");
            unitOfWorkMock.Setup(x => x.CommitTransaction()).Throws(new InvalidOperationException("commit failed"));

            // act
            filter.OnActionExecuted(context);

            // assert
            Assert.Null(context.Result);
        }

        [Fact]
        public void When_Commit_Throws_Should_Not_Propagate_The_Exception()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            unitOfWorkMock.Setup(x => x.CommitTransaction()).Throws(new InvalidOperationException("commit failed"));

            // act
            var exception = Record.Exception(() => filter.OnActionExecuted(context));

            // assert
            Assert.Null(exception);
        }

        [Fact]
        public void When_Both_Commit_And_Rollback_Throw_Should_Report_Both_Without_Throwing()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            var commitException = new InvalidOperationException("commit failed");
            var rollbackException = new InvalidOperationException("rollback failed");
            unitOfWorkMock.Setup(x => x.CommitTransaction()).Throws(commitException);
            unitOfWorkMock.Setup(x => x.RollbackTransaction()).Throws(rollbackException);

            // act
            var exception = Record.Exception(() => filter.OnActionExecuted(context));

            // assert - the original commit failure stays visible, the rollback failure is attached
            Assert.Null(exception);
            var aggregate = Assert.IsType<AggregateException>(context.Exception);
            Assert.Contains(commitException, aggregate.InnerExceptions);
            Assert.Contains(rollbackException, aggregate.InnerExceptions);
            Assert.Null(context.Result);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(409)]
        [InlineData(422)]
        [InlineData(500)]
        public void When_Action_Returned_Error_Status_Should_Rollback_And_Not_Commit(int statusCode)
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            var originalResult = new StatusCodeResult(statusCode);
            context.Result = originalResult;

            // act
            filter.OnActionExecuted(context);

            // assert - partial work behind an error response is never persisted
            unitOfWorkMock.Verify(x => x.RollbackTransaction(), Times.Once);
            unitOfWorkMock.Verify(x => x.CommitTransaction(), Times.Never);
            Assert.Same(originalResult, context.Result);
        }

        [Theory]
        [InlineData(200)]
        [InlineData(201)]
        [InlineData(204)]
        [InlineData(302)]
        public void When_Action_Returned_Non_Error_Status_Should_Commit(int statusCode)
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            context.Result = new StatusCodeResult(statusCode);

            // act
            filter.OnActionExecuted(context);

            // assert
            unitOfWorkMock.Verify(x => x.CommitTransaction(), Times.Once);
            unitOfWorkMock.Verify(x => x.RollbackTransaction(), Times.Never);
        }

        [Fact]
        public void When_Action_Returned_Object_Result_With_Error_Status_Should_Rollback()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutedContext();
            context.Result = new ObjectResult("conflict") { StatusCode = 409 };

            // act
            filter.OnActionExecuted(context);

            // assert
            unitOfWorkMock.Verify(x => x.RollbackTransaction(), Times.Once);
            unitOfWorkMock.Verify(x => x.CommitTransaction(), Times.Never);
        }

        #endregion

        #region OnActionExecuting

        [Fact]
        public void When_Action_Has_IsolationLevel_Attribute_Should_Set_Isolation_Level_From_Attribute()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutingContext(nameof(DummyController.SerializableAction));

            // act
            filter.OnActionExecuting(context);

            // assert
            unitOfWorkMock.Verify(x => x.SetIsolationLevel(IsolationLevel.Serializable), Times.Once);
            unitOfWorkMock.Verify(x => x.SetIsolationLevel(It.IsAny<IsolationLevel>()), Times.Once);
        }

        [Fact]
        public void When_Action_Has_No_IsolationLevel_Attribute_Should_Not_Set_Isolation_Level()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutingContext(nameof(DummyController.PlainAction));

            // act
            filter.OnActionExecuting(context);

            // assert
            unitOfWorkMock.Verify(x => x.SetIsolationLevel(It.IsAny<IsolationLevel>()), Times.Never);
        }

        [Fact]
        public void When_ActionDescriptor_Is_Not_ControllerActionDescriptor_Should_Not_Set_Isolation_Level()
        {
            // arrange
            var context = FilterContextFactory.CreateExecutingContext(FilterContextFactory.CreateNonControllerActionContext());

            // act
            filter.OnActionExecuting(context);

            // assert
            unitOfWorkMock.Verify(x => x.SetIsolationLevel(It.IsAny<IsolationLevel>()), Times.Never);
        }

        [Fact]
        public void When_Action_Is_Executing_Should_Not_Begin_Transaction_Manually()
        {
            // arrange
            // The filter never opens the transaction itself - the Unit of Work opens it lazily.
            var context = FilterContextFactory.CreateExecutingContext(nameof(DummyController.SerializableAction));

            // act
            filter.OnActionExecuting(context);

            // assert
            unitOfWorkMock.Verify(x => x.BeginTransactionManually(), Times.Never);
        }

        #endregion
    }
}
