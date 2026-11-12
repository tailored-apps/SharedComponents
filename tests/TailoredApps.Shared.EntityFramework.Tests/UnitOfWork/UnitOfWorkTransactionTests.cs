using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.Tests.UnitOfWork;

/// <summary>Transaction handling of <see cref="UnitOfWork{T}"/>: when a transaction opens, how it ends, and the isolation level.</summary>
public class UnitOfWorkTransactionTests
{
    private readonly Mock<IUnitOfWorkContext> context = new();
    private readonly Mock<IHooksManager> hooks = new();
    private readonly Mock<ITransaction> transaction = new();
    private readonly Mock<DbConnection> connection = new();
    private readonly Mock<DbCommand> command = new();
    private readonly object dataProvider = new();

    public UnitOfWorkTransactionTests()
    {
        command.SetupProperty(c => c.CommandText);
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        context.Setup(c => c.GetDbConnection()).Returns(connection.Object);
        context.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(transaction.Object);
    }

    private UnitOfWork<object> CreateSut() => new(context.Object, dataProvider, hooks.Object);

    private void OpenConnection(ConnectionState from = ConnectionState.Closed, ConnectionState to = ConnectionState.Open)
        => connection.Raise(c => c.StateChange += null, connection.Object, new StateChangeEventArgs(from, to));

    [Fact]
    public void When_Created_Should_Expose_The_Data_Provider_And_Have_No_Transaction()
    {
        var sut = CreateSut();

        Assert.Same(dataProvider, sut.DataProvider);
        Assert.False(sut.HasOpenTransaction);
        context.Verify(c => c.BeginTransaction(It.IsAny<IsolationLevel>()), Times.Never);
    }

    [Fact]
    public void When_Transaction_Is_Begun_Manually_Should_Open_One_With_ReadCommitted()
    {
        var sut = CreateSut();

        sut.BeginTransactionManually();

        Assert.True(sut.HasOpenTransaction);
        context.Verify(c => c.BeginTransaction(IsolationLevel.ReadCommitted), Times.Once);
    }

    [Fact]
    public void When_Transaction_Is_Already_Open_Should_Not_Open_Another()
    {
        var sut = CreateSut();

        sut.BeginTransactionManually();
        sut.BeginTransactionManually();
        sut.SaveChanges();

        context.Verify(c => c.BeginTransaction(It.IsAny<IsolationLevel>()), Times.Once);
    }

    [Fact]
    public void When_Changes_Are_Saved_Should_Open_A_Transaction_And_Return_The_Number_Of_Rows()
    {
        context.Setup(c => c.SaveChanges()).Returns(3);
        var sut = CreateSut();

        var saved = sut.SaveChanges();

        Assert.Equal(3, saved);
        Assert.True(sut.HasOpenTransaction);
    }

    [Fact]
    public async Task When_Changes_Are_Saved_Asynchronously_Should_Pass_The_Cancellation_Token()
    {
        using var cancellation = new CancellationTokenSource();
        context.Setup(c => c.SaveChangesAsync(cancellation.Token)).ReturnsAsync(7);
        var sut = CreateSut();

        var saved = await sut.SaveChangesAsync(cancellation.Token);

        Assert.Equal(7, saved);
        Assert.True(sut.HasOpenTransaction);
    }

    [Fact]
    public void When_Transaction_Is_Committed_Should_Save_Commit_Dispose_And_Run_Commit_Hooks()
    {
        var sut = CreateSut();

        sut.CommitTransaction();

        context.Verify(c => c.SaveChanges(), Times.Once);
        transaction.Verify(t => t.Commit(), Times.Once);
        transaction.Verify(t => t.Dispose(), Times.Once);
        hooks.Verify(h => h.ExecuteTransactionCommitHooks(), Times.Once);
        Assert.False(sut.HasOpenTransaction);
    }

    [Fact]
    public void When_Commit_Fails_Should_Dispose_The_Transaction_And_Skip_Commit_Hooks()
    {
        transaction.Setup(t => t.Commit()).Throws(new InvalidOperationException("deadlock"));
        var sut = CreateSut();

        Assert.Throws<InvalidOperationException>(() => sut.CommitTransaction());

        transaction.Verify(t => t.Dispose(), Times.Once);
        hooks.Verify(h => h.ExecuteTransactionCommitHooks(), Times.Never);
        Assert.False(sut.HasOpenTransaction);
    }

    [Fact]
    public void When_Open_Transaction_Is_Rolled_Back_Should_Roll_Back_Dispose_Discard_Changes_And_Run_Rollback_Hooks()
    {
        var sut = CreateSut();
        sut.BeginTransactionManually();

        sut.RollbackTransaction();

        transaction.Verify(t => t.Rollback(), Times.Once);
        transaction.Verify(t => t.Dispose(), Times.Once);
        context.Verify(c => c.DiscardChanges(), Times.Once);
        hooks.Verify(h => h.ExecuteTransactionRollbackHooks(), Times.Once);
        Assert.False(sut.HasOpenTransaction);
    }

    [Fact]
    public void When_Rollback_Fails_Should_Still_Dispose_Discard_Changes_And_Run_Rollback_Hooks()
    {
        transaction.Setup(t => t.Rollback()).Throws(new InvalidOperationException("connection lost"));
        var sut = CreateSut();
        sut.BeginTransactionManually();

        Assert.Throws<InvalidOperationException>(() => sut.RollbackTransaction());

        transaction.Verify(t => t.Dispose(), Times.Once);
        context.Verify(c => c.DiscardChanges(), Times.Once);
        hooks.Verify(h => h.ExecuteTransactionRollbackHooks(), Times.Once);
        Assert.False(sut.HasOpenTransaction);
    }

    [Fact]
    public void When_Nothing_Is_Open_Should_Roll_Back_Only_The_Tracked_Changes()
    {
        var sut = CreateSut();

        sut.RollbackTransaction();

        transaction.Verify(t => t.Rollback(), Times.Never);
        context.Verify(c => c.DiscardChanges(), Times.Once);
    }

    [Fact]
    public void When_Isolation_Level_Is_Set_Should_Use_It_For_The_Next_Transaction()
    {
        var sut = CreateSut();

        sut.SetIsolationLevel(IsolationLevel.Serializable);
        sut.BeginTransactionManually();

        context.Verify(c => c.BeginTransaction(IsolationLevel.Serializable), Times.Once);
    }

    [Fact]
    public void When_Committed_With_An_Isolation_Level_Should_Apply_It_To_The_Transaction_That_Follows()
    {
        var sut = CreateSut();

        sut.CommitTransaction(IsolationLevel.ReadUncommitted);
        sut.BeginTransactionManually();

        context.Verify(c => c.BeginTransaction(IsolationLevel.ReadCommitted), Times.Once);
        context.Verify(c => c.BeginTransaction(IsolationLevel.ReadUncommitted), Times.Once);
    }

    [Fact]
    public void When_Rolled_Back_With_An_Isolation_Level_Should_Apply_It_To_The_Transaction_That_Follows()
    {
        var sut = CreateSut();

        sut.RollbackTransaction(IsolationLevel.Serializable);
        sut.BeginTransactionManually();

        context.Verify(c => c.BeginTransaction(IsolationLevel.Serializable), Times.Once);
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted, "SET TRANSACTION ISOLATION LEVEL READ COMMITTED")]
    [InlineData(IsolationLevel.ReadUncommitted, "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED")]
    [InlineData(IsolationLevel.Serializable, "SET TRANSACTION ISOLATION LEVEL SERIALIZABLE")]
    public void When_Connection_Opens_Should_Set_The_Isolation_Level_On_It(IsolationLevel isolationLevel, string expectedCommand)
    {
        var sut = CreateSut();
        sut.SetIsolationLevel(isolationLevel);

        OpenConnection();

        Assert.Equal(expectedCommand, command.Object.CommandText);
        command.Verify(c => c.ExecuteNonQuery(), Times.Once);
    }

    [Fact]
    public void When_Connection_Opens_With_An_Unsupported_Isolation_Level_Should_Throw()
    {
        var sut = CreateSut();
        sut.SetIsolationLevel(IsolationLevel.Chaos);

        Assert.Throws<ArgumentOutOfRangeException>(() => OpenConnection());

        command.Verify(c => c.ExecuteNonQuery(), Times.Never);
    }

    [Theory]
    [InlineData(ConnectionState.Open, ConnectionState.Closed)]
    [InlineData(ConnectionState.Open, ConnectionState.Open)]
    [InlineData(ConnectionState.Closed, ConnectionState.Connecting)]
    public void When_Connection_Changes_State_Without_Opening_Should_Not_Send_A_Command(ConnectionState from, ConnectionState to)
    {
        CreateSut();

        OpenConnection(from, to);

        command.Verify(c => c.ExecuteNonQuery(), Times.Never);
    }

    [Fact]
    public void When_Disposed_Should_Dispose_The_Open_Transaction_Once()
    {
        var sut = CreateSut();
        sut.BeginTransactionManually();

        sut.Dispose();
        sut.Dispose();

        transaction.Verify(t => t.Dispose(), Times.Once);
        Assert.False(sut.HasOpenTransaction);
    }
}
