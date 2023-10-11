using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Threading.Tasks;
using TailoredApps.Shared.Payments.Provider.CashBill;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests
{
    public class PaymentTest
    {

        [Fact]
        public async Task CanRequestPaymentProvidersEmpty()
        {
            var host = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration(a => a.AddEnvironmentVariables())
                 .ConfigureServices((_, services) =>
                 {
                     services.AddPayments();
                 }).Build();

            var paymentService = host.Services.GetService<IPaymentService>();
            var providers = await paymentService.GetProviders();
            Assert.Empty(providers);
        }


        [Fact]
        public async Task CanRequestPaymentProvidersOnCashbillAndRequestTransaction()
        {
            var host = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration(a => a.AddEnvironmentVariables())
                 .ConfigureServices((_, services) =>
                 {
                     services.RegisterCashbillProvider();
                     services.AddPayments()
                        .RegisterPaymentProvider<CashBillProvider>();
                 });
            var builder = host.Build();
            var paymentService = builder.Services.GetService<IPaymentService>();
            var providers = await paymentService.GetProviders();
            var cashbill = await paymentService.GetProviders();
            var cashbillChannels = await paymentService.GetChannels(cashbill.Single(z => z.Id == "Cashbill").Id, "PLN");

            Assert.NotEmpty(cashbillChannels);


            var payment = await paymentService.RegisterPayment(new PaymentRequest
            {
                PaymentProvider = cashbill.Single(z => z.Id == "Cashbill").Id,
                PaymentChannel = cashbillChannels.Single().Id,
                Amount = 1.29m,
                AdditionalData = "na fajki",
                City = "Kraśnik",
                Country = "Polska",
                Currency = "PLN",
                Description = "Kraśnik wolny od 5G",
                Email = "kaczynski.to@i.caly.pis.pl",
                FirstName = "Vateusz",
                Surname = "Morawiecki",
                Flat = "666",
                House = "333",
                PaymentModel = PaymentModel.OneTime,
                PostCode = "99-666",
                Referer = "Sylwester marzeń",
                Street = "Kurska 20",
                Title = "Pięć gwiazdek trzy gwiazdki i konfederację!"
            });

            Assert.NotNull(payment);
            Assert.NotEmpty(payment.RedirectUrl);
            Assert.Equal(PaymentStatusEnum.Created, payment.PaymentStatus);

            var status = await paymentService.GetStatus(cashbill.Single(z => z.Id == "Cashbill").Id, payment.PaymentUniqueId);
            Assert.NotNull(status);
            Assert.Equal(PaymentStatusEnum.Created, status.PaymentStatus);
        }


        [Fact]
        public async Task TransactionChange()
        {
            var host = Host.CreateDefaultBuilder()
                 .ConfigureAppConfiguration(a => a.AddEnvironmentVariables())
                 .ConfigureServices((_, services) =>
                 {
                     services.RegisterCashbillProvider();
                     services.AddPayments()
                        .RegisterPaymentProvider<CashBillProvider>();
                 });
            var builder = host.Build();
            var paymentService = builder.Services.GetService<IPaymentService>();
            var providers = await paymentService.GetProviders();
            var cashbill = providers.Single(z => z.Id == "Cashbill");
            var cashbillChannels = await paymentService.GetChannels(cashbill.Id, "PLN");

            Assert.NotEmpty(cashbillChannels);


            var payment = paymentService.TransactionStatusChange(cashbill.Id, new TransactionStatusChangePayload
            {
                Payload = null,
                ProviderId = cashbill.Id,
                QueryParameters = new System.Collections.Generic.Dictionary<string, Microsoft.Extensions.Primitives.StringValues> {
                    { "cmd", new Microsoft.Extensions.Primitives.StringValues("transactionStatusChanged") },
                    { "args", new Microsoft.Extensions.Primitives.StringValues("TEST_6f7zsddbw") },
                    { "sign", new Microsoft.Extensions.Primitives.StringValues("2050dc9f7149ef52d07f621d7d0d41b6") }
                }
            });

            Assert.NotNull(payment);
        }
    }
}
