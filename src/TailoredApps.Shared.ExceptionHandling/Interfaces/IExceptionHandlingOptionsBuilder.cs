using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.ExceptionHandling.Interfaces
{
    /// <summary>Builder do konfigurowania dostawców obsługi wyjątków w DI.</summary>
    public interface IExceptionHandlingOptionsBuilder
    {
        /// <summary>Rejestruje dostawcę obsługi wyjątków (automatyczna aktywacja przez DI).</summary>
        IExceptionHandlingOptionsBuilder WithExceptionHandlingProvider<Provider>() where Provider : class, IExceptionHandlingProvider;

        /// <summary>Rejestruje dostawcę obsługi wyjątków z fabryczną metodą tworzenia instancji.</summary>
        IExceptionHandlingOptionsBuilder WithExceptionHandlingProvider<Provider>(Func<IServiceProvider, Provider> implementationFactory) where Provider : class, IExceptionHandlingProvider;

        /// <summary>Kolekcja usług DI, do której rejestrowane są dostawcy.</summary>
        IServiceCollection Services { get; }
    }
}
