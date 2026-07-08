using System;
using Microsoft.Extensions.DependencyInjection;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Fluent builder for registering payment providers with the DI container.
    /// Returned by <c>AddPayments()</c> extension methods on <see cref="IServiceCollection"/>.
    /// </summary>
    public interface IPaymentOptionsBuilder
    {
        /// <summary>
        /// Registers a payment provider type using the default transient lifetime.
        /// </summary>
        /// <typeparam name="TPaymentProvider">Concrete provider type that implements <see cref="IPaymentProvider"/>.</typeparam>
        /// <returns>The same builder instance for method chaining.</returns>
        IPaymentOptionsBuilder RegisterPaymentProvider<TPaymentProvider>() where TPaymentProvider : class, IPaymentProvider;

        /// <summary>
        /// Registers a payment provider using a custom factory delegate.
        /// </summary>
        /// <typeparam name="TPaymentProvider">Concrete provider type that implements <see cref="IPaymentProvider"/>.</typeparam>
        /// <param name="implementationFactory">Factory delegate that creates the provider instance from the service provider.</param>
        /// <returns>The same builder instance for method chaining.</returns>
        IPaymentOptionsBuilder RegisterPaymentProvider<TPaymentProvider>(Func<IServiceProvider, TPaymentProvider> implementationFactory) where TPaymentProvider : class, IPaymentProvider;

        /// <summary>Gets the underlying <see cref="IServiceCollection"/> for direct DI configuration.</summary>
        IServiceCollection Services { get; }
    }
}
