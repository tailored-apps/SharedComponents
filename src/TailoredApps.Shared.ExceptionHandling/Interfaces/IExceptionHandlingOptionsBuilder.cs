using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.ExceptionHandling.Interfaces
{
    public interface IExceptionHandlingOptionsBuilder
    {
        IExceptionHandlingOptionsBuilder WithExceptionHandlingProvider<Provider>() where Provider : class, IExceptionHandlingProvider;
        IExceptionHandlingOptionsBuilder WithExceptionHandlingProvider<Provider>(Func<IServiceProvider, Provider> implementationFactory) where Provider : class, IExceptionHandlingProvider;
        IServiceCollection Services { get; }
    }
}
