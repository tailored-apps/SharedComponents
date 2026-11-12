using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TailoredApps.Shared.ExceptionHandling.Interfaces;
using TailoredApps.Shared.ExceptionHandling.Providers;
using TailoredApps.Shared.ExceptionHandling.WebApiCore;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Attributes;
using TailoredApps.Shared.ExceptionHandling.WebApiCore.Filters;
using Xunit;

namespace TailoredApps.Shared.ExceptionHandling.Tests;

public class ExceptionHandlingConfigurationTests
{
    [Fact]
    public void When_AddExceptionHandlingForWebApi_Is_Called_Should_Return_Builder_With_Same_Services()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        var builder = services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();

        // assert
        Assert.NotNull(builder);
        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void When_AddExceptionHandlingForWebApi_Is_Called_Should_Register_Resolvable_Services()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddExceptionHandlingForWebApi<IExceptionHandlingProvider, DefaultExceptionHandlingProvider>();

        // assert
        using var rootProvider = services.BuildServiceProvider();
        using var scope = rootProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        var service = scopedProvider.GetRequiredService<IExceptionHandlingService>();
        var typedService = Assert.IsType<ExceptionHandlingService<DefaultExceptionHandlingProvider>>(service);
        Assert.IsType<DefaultExceptionHandlingProvider>(typedService.ExceptionHandlingProvider);

        Assert.NotNull(scopedProvider.GetRequiredService<HandleExceptionAttribute>());
        Assert.IsType<DefaultExceptionHandlingProvider>(scopedProvider.GetRequiredService<IExceptionHandlingProvider>());
        Assert.NotNull(scopedProvider.GetRequiredService<DefaultExceptionHandlingProvider>());
    }

    [Fact]
    public void When_AddExceptionHandlingFilterAttribute_Is_Called_Should_Add_Filter_To_Collection()
    {
        // arrange
        var filters = new FilterCollection();

        // act
        filters.AddExceptionHandlingFilterAttribute();

        // assert
        var filter = Assert.Single(filters);
        var typeFilter = Assert.IsType<TypeFilterAttribute>(filter);
        Assert.Equal(typeof(HandleExceptionFilterAttribute), typeFilter.ImplementationType);
    }

    [Fact]
    public void When_The_Library_Is_Inspected_Should_Have_No_Obsolete_Public_Members()
    {
        // 3.0 removed the misspelled AddExceptionHAndlingFilterAttribute; nothing obsolete is left to remove.
        var obsolete = typeof(ExceptionHandlingConfiguration).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers().Prepend(type))
            .Where(member => member.IsDefined(typeof(ObsoleteAttribute), inherit: false))
            .Select(member => $"{member.DeclaringType?.Name}.{member.Name}");

        Assert.Empty(obsolete);
    }
}
