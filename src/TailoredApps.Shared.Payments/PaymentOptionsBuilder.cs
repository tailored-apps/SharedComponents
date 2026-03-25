using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.Payments
{
    /// <summary>
    /// Default implementation of <see cref="IPaymentOptionsBuilder"/>.
    /// Provides a fluent API for registering payment providers during application startup.
    /// </summary>
    public class PaymentOptionsBuilder : IPaymentOptionsBuilder
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PaymentOptionsBuilder"/> with the given service collection.
        /// </summary>
        /// <param name="serviceCollection">The DI service collection to register providers into.</param>
        public PaymentOptionsBuilder(IServiceCollection serviceCollection)
        {
            Services = serviceCollection;
        }

        /// <inheritdoc/>
        public IServiceCollection Services { get; private set; }

        /// <inheritdoc/>
        public IPaymentOptionsBuilder RegisterPaymentProvider<TPaymentProvider>() where TPaymentProvider : class, IPaymentProvider
            => WithPaymentProvider<TPaymentProvider>();

        /// <inheritdoc/>
        public IPaymentOptionsBuilder RegisterPaymentProvider<TPaymentProvider>(Func<IServiceProvider, TPaymentProvider> implementationFactory) where TPaymentProvider : class, IPaymentProvider
            => WithPaymentProvider(implementationFactory);


        private IPaymentOptionsBuilder WithPaymentProvider<TPaymentProvider>() where TPaymentProvider : class, IPaymentProvider
        {

            Services.AddTransient<IPaymentProvider, TPaymentProvider>();
            return this;
        }

        private IPaymentOptionsBuilder WithPaymentProvider<TPaymentProvider>(Func<IServiceProvider, TPaymentProvider> implementationFactory)
            where TPaymentProvider : class, IPaymentProvider
        {
            Services.AddTransient<IPaymentProvider, TPaymentProvider>(implementationFactory);
            return this;
        }
    }



    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to configure the payments infrastructure.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the standard payments infrastructure using the built-in
        /// <see cref="IPaymentService"/> / <see cref="PaymentService"/> pair.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <returns>An <see cref="IPaymentOptionsBuilder"/> for registering individual providers.</returns>
        public static IPaymentOptionsBuilder AddPayments(this IServiceCollection services)
        {

            return services.AddPaymentsForWebApi<IPaymentService, PaymentService>();

        }

        /// <summary>
        /// Registers the payments infrastructure with a custom service interface and implementation,
        /// suitable for Web API projects where scoped lifetime is required.
        /// </summary>
        /// <typeparam name="TTargetPaymentServiceInterface">Custom payment service interface.</typeparam>
        /// <typeparam name="TTargetPaymentService">Concrete implementation of the custom interface.</typeparam>
        /// <param name="services">The DI service collection.</param>
        /// <returns>An <see cref="IPaymentOptionsBuilder"/> for registering individual providers.</returns>
        public static IPaymentOptionsBuilder AddPaymentsForWebApi<TTargetPaymentServiceInterface, TTargetPaymentService>(this IServiceCollection services)
            where TTargetPaymentService : class, TTargetPaymentServiceInterface
            where TTargetPaymentServiceInterface : class
        {
            return services.AddPayments<TTargetPaymentServiceInterface, TTargetPaymentService>();
        }

        /// <summary>
        /// Core registration helper — registers the payment service with scoped lifetime
        /// under both the concrete type and the specified interface.
        /// </summary>
        /// <typeparam name="TTargetPaymentServiceInterface">Service interface to expose.</typeparam>
        /// <typeparam name="TTargetPaymentService">Concrete implementation type.</typeparam>
        /// <param name="services">The DI service collection.</param>
        /// <returns>An <see cref="IPaymentOptionsBuilder"/> for registering individual providers.</returns>
        public static IPaymentOptionsBuilder AddPayments<TTargetPaymentServiceInterface, TTargetPaymentService>(this IServiceCollection services)
            where TTargetPaymentService : class, TTargetPaymentServiceInterface
            where TTargetPaymentServiceInterface : class
        {
            services.AddScoped<TTargetPaymentService, TTargetPaymentService>();
            services.AddScoped<TTargetPaymentServiceInterface, TTargetPaymentService>(container => container.GetRequiredService<TTargetPaymentService>());

            //services.AddScoped<IUnitOfWorkContext, UnitOfWorkContext<TTargetPaymentService>>();
            //services.AddScoped<UnitOfWork<TTargetPaymentServiceInterface>>();
            //services.AddScoped<IUnitOfWork>(container => container.GetRequiredService<UnitOfWork<TTargetPaymentServiceInterface>>());
            //services.AddScoped<IUnitOfWork<TTargetPaymentServiceInterface>>(container => container.GetRequiredService<UnitOfWork<TTargetPaymentServiceInterface>>());
            //services.AddTransient<IHooksManager, UnitOfWorkHooksManager>();

            return new PaymentOptionsBuilder(services);
        }
    }
}
