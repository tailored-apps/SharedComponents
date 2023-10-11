using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.Payments
{
    public class PaymentOptionsBuilder : IPaymentOptionsBuilder
    {
        public PaymentOptionsBuilder(IServiceCollection serviceCollection)
        {
            Services = serviceCollection;
        }

        public IServiceCollection Services { get; private set; }

        public IPaymentOptionsBuilder RegisterPaymentProvider<TPaymentProvider>() where TPaymentProvider : class, IPaymentProvider
            => WithPaymentProvider<TPaymentProvider>();

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



    public static class ServiceCollectionExtensions
    {
        public static IPaymentOptionsBuilder AddPayments(this IServiceCollection services)
        {

            return services.AddPaymentsForWebApi<IPaymentService, PaymentService>();

        }

        public static IPaymentOptionsBuilder AddPaymentsForWebApi<TTargetPaymentServiceInterface, TTargetPaymentService>(this IServiceCollection services)
            where TTargetPaymentService : class, TTargetPaymentServiceInterface
            where TTargetPaymentServiceInterface : class
        {
            return services.AddPayments<TTargetPaymentServiceInterface, TTargetPaymentService>();
        }

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
