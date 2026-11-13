using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using TailoredApps.Shared.EntityFramework.Interfaces.UnitOfWork;
using TailoredApps.Shared.EntityFramework.UnitOfWork.MinimalApi;
using Xunit;

namespace TailoredApps.Shared.EntityFramework.UnitOfWork.WebApiCore.Tests;

/// <summary>
/// The minimal-API transaction filter in a real pipeline, with a mocked unit of work: what is committed and
/// what is rolled back for every way an endpoint can end. The rules are those of the MVC transaction filter.
/// </summary>
public class UnitOfWorkTransactionEndpointFilterTests
{
    [Fact]
    public async Task When_The_Endpoint_Returns_An_Object_Should_Commit()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/ok", () => "done").WithUnitOfWorkTransaction());

        var response = await host.GetTestClient().GetAsync("/ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        unitOfWork.Verify(u => u.CommitTransaction(), Times.Once);
        unitOfWork.Verify(u => u.RollbackTransaction(), Times.Never);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(302)]
    public async Task When_The_Endpoint_Returns_A_Result_Below_400_Should_Commit(int status)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/r", () => Results.StatusCode(status)).WithUnitOfWorkTransaction());

        await host.GetTestClient().GetAsync("/r");

        unitOfWork.Verify(u => u.CommitTransaction(), Times.Once);
        unitOfWork.Verify(u => u.RollbackTransaction(), Times.Never);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task When_The_Endpoint_Returns_An_Error_Result_Should_Roll_Back_And_Keep_The_Answer(int status)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/r", () => Results.StatusCode(status)).WithUnitOfWorkTransaction());

        var response = await host.GetTestClient().GetAsync("/r");

        Assert.Equal(status, (int)response.StatusCode);
        unitOfWork.Verify(u => u.RollbackTransaction(), Times.Once);
        unitOfWork.Verify(u => u.CommitTransaction(), Times.Never);
    }

    [Fact]
    public async Task When_The_Endpoint_Throws_Should_Roll_Back_And_Rethrow_The_Same_Exception()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var thrown = new InvalidOperationException("the endpoint failed");
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/boom", string () => throw thrown).WithUnitOfWorkTransaction());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.GetTestClient().GetAsync("/boom"));

        Assert.Same(thrown, exception);
        unitOfWork.Verify(u => u.RollbackTransaction(), Times.Once);
        unitOfWork.Verify(u => u.CommitTransaction(), Times.Never);
    }

    [Fact]
    public async Task When_The_Commit_Fails_Should_Roll_Back_And_Throw_The_Commit_Exception()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.CommitTransaction()).Throws(new DbUpdateException("the commit failed"));
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/ok", () => "done").WithUnitOfWorkTransaction());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => host.GetTestClient().GetAsync("/ok"));

        Assert.Equal("the commit failed", exception.Message);
        unitOfWork.Verify(u => u.RollbackTransaction(), Times.Once);
    }

    [Fact]
    public async Task When_The_Commit_And_The_Rollback_Fail_Should_Report_Both()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.CommitTransaction()).Throws(new DbUpdateException("the commit failed"));
        unitOfWork.Setup(u => u.RollbackTransaction()).Throws(new InvalidOperationException("the rollback failed"));
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/ok", () => "done").WithUnitOfWorkTransaction());

        var exception = await Assert.ThrowsAsync<AggregateException>(() => host.GetTestClient().GetAsync("/ok"));

        Assert.Collection(exception.InnerExceptions,
            first => Assert.IsType<DbUpdateException>(first),
            second => Assert.Equal("the rollback failed", second.Message));
    }

    [Fact]
    public async Task When_An_Isolation_Level_Is_Given_Should_Set_It_Before_The_Endpoint_Runs()
    {
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var sequence = new MockSequence();
        unitOfWork.InSequence(sequence).Setup(u => u.SetIsolationLevel(IsolationLevel.Serializable));
        unitOfWork.InSequence(sequence).Setup(u => u.CommitTransaction());
        unitOfWork.Setup(u => u.Dispose());
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/ok", () => "done").WithUnitOfWorkTransaction(IsolationLevel.Serializable));

        var response = await host.GetTestClient().GetAsync("/ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        unitOfWork.Verify(u => u.SetIsolationLevel(IsolationLevel.Serializable), Times.Once);
    }

    [Fact]
    public async Task When_No_Isolation_Level_Is_Given_Should_Leave_The_Default()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        using var host = await StartAsync(unitOfWork, endpoints => endpoints.MapGet("/ok", () => "done").WithUnitOfWorkTransaction());

        await host.GetTestClient().GetAsync("/ok");

        unitOfWork.Verify(u => u.SetIsolationLevel(It.IsAny<IsolationLevel>()), Times.Never);
    }

    [Fact]
    public async Task When_The_Group_Has_The_Filter_Should_Apply_It_To_Every_Endpoint_And_Only_Those()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        using var host = await StartAsync(unitOfWork, endpoints =>
        {
            var group = endpoints.MapGroup("/api").WithUnitOfWorkTransaction();
            group.MapGet("/a", () => "a");
            group.MapGet("/b", () => Results.BadRequest());
            endpoints.MapGet("/outside", () => "no transaction");
        });
        var client = host.GetTestClient();

        await client.GetAsync("/api/a");
        await client.GetAsync("/api/b");
        await client.GetAsync("/outside");

        unitOfWork.Verify(u => u.CommitTransaction(), Times.Once);
        unitOfWork.Verify(u => u.RollbackTransaction(), Times.Once);
    }

    [Fact]
    public void When_Registered_For_Minimal_Api_Should_Register_The_Unit_Of_Work()
    {
        var services = new ServiceCollection();

        var builder = services.AddUnitOfWorkForMinimalApi<IEmptyContext, EmptyContext>();

        Assert.NotNull(builder);
        Assert.Contains(services, s => s.ServiceType == typeof(IUnitOfWork));
        Assert.Contains(services, s => s.ServiceType == typeof(IUnitOfWork<IEmptyContext>));
    }

    public interface IEmptyContext;

    public class EmptyContext(DbContextOptions<EmptyContext> options) : DbContext(options), IEmptyContext;

    private static async Task<IHost> StartAsync(Mock<IUnitOfWork> unitOfWork, Action<IEndpointRouteBuilder> map)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddScoped(_ => unitOfWork.Object);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(map);
                }))
            .Build();
        await host.StartAsync();
        return host;
    }
}
