using Microsoft.Extensions.DependencyInjection;
using System;

namespace TailoredApps.Shared.Payments
{
    public interface IPaymentOptionsBuilder
    {
        IPaymentOptionsBuilder RegisterPaymentProvider<TPaymentProvider>() where TPaymentProvider : class, IPaymentProvider;
        IPaymentOptionsBuilder RegisterPaymentProvider<TPaymentProvider>(Func<IServiceProvider, TPaymentProvider> implementationFactory) where TPaymentProvider : class, IPaymentProvider;


        IServiceCollection Services { get; }
    }
}
