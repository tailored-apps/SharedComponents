using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;
using static TailoredApps.Shared.Payments.Provider.CashBill.CashbillServiceCaller;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    public class CashBillProvider : IPaymentProvider
    {
        private readonly ICashbillServiceCaller cashbillService;
        public CashBillProvider(ICashbillServiceCaller cashbillService)
        {
            this.cashbillService = cashbillService;
        }
        private static string CashBillProviderKey = "Cashbill";
        public string Key => CashBillProviderKey;

        public string Name => CashBillProviderKey;

        public string Description => "Polski operator płatności jednorazowych.";

        public string Url => "https://cashbill.pl";

        public async Task<ICollection<PaymentChannel>> GetPaymentChannels(string currency)
        {
            var channels = await cashbillService.GetPaymentChannels(currency);
            return channels.Select(x => new PaymentChannel
            {
                AvailableCurrencies = x.AvailableCurrencies,
                Id = x.Id,
                Description = x.Description,
                LogoUrl = x.LogoUrl,
                Name = x.Name,
                PaymentModel = PaymentModel.OneTime
            }
            ).ToList();
        }

        public async Task<PaymentResponse> RequestPayment(Payments.PaymentRequest request)
        {
            var payment = await cashbillService.GeneratePayment(new PaymentRequest
            {
                Referer = request.Referer,
                AdditionalData = request.AdditionalData,
                Amount = request.Amount,
                City = request.City,
                Country = request.Country,
                Currency = request.Currency,
                Description = request.Description,
                Email = request.Email,
                FirstName = request.FirstName,
                Flat = request.Flat,
                House = request.House,
                PaymentChannel = request.PaymentChannel,
                PostCode = request.PostCode,
                Street = request.Street,
                Surname = request.Surname,
                Title = request.Title
            });

            return new PaymentResponse { PaymentUniqueId = payment.Id, RedirectUrl = payment.PaymentProviderRedirectUrl, PaymentStatus = GetPaymentStatus(payment.Status) };
        }


        public async Task<PaymentResponse> GetStatus(string paymentId)
        {
            var payment = await cashbillService.GetPaymentStatus(paymentId);

            return new PaymentResponse { PaymentUniqueId = payment.Id, RedirectUrl = payment.PaymentProviderRedirectUrl, PaymentStatus = GetPaymentStatus(payment.Status) };
        }

        private PaymentStatusEnum GetPaymentStatus(string status)
        {

            IDictionary<PaymentStatusEnum, IList<string>> statuses = new Dictionary<PaymentStatusEnum, IList<string>>
            {
                { PaymentStatusEnum.Created, new List<string> { PaymentStatusConst.Start, PaymentStatusConst.PreStart } },
                { PaymentStatusEnum.Processing, new List<string> { PaymentStatusConst.PositiveAuthorization , PaymentStatusConst.NegativeAuthorization} },
                { PaymentStatusEnum.Finished, new List<string> { PaymentStatusConst.PositiveFinish } },
                { PaymentStatusEnum.Rejected, new List<string> {
                                                                 PaymentStatusConst.NegativeFinish,
                                                                 PaymentStatusConst.CriticalError,
                                                                 PaymentStatusConst.Abort,
                                                                 PaymentStatusConst.Fraud,
                                                                 PaymentStatusConst.TimeExceeded
                                                                } }
            };

            var key = statuses.Single(x => x.Value.Any(z => z == status)).Key;
            return key;
        }

        public async Task<PaymentResponse> TransactionStatusChange(TransactionStatusChangePayload payload)
        {
            var request = new TransactionStatusChanged
            {
                Command = payload.QueryParameters["cmd"].ToString(),
                TransactionId = payload.QueryParameters["args"].ToString(),
                Sign = payload.QueryParameters["sign"].ToString(),
            };
            //TODO: nie wiedząc czemu coś weryfikacja klucza kuleje.  trzeba sprawdzić na innym sklepie czy zadziała?
            //var sign = await cashbillService.GetSignForNotificationService(request);
            ////if (request.Sign == sign)
            //{
            var status = await cashbillService.GetPaymentStatus(request.TransactionId);

            return new PaymentResponse { PaymentUniqueId = status.Id, RedirectUrl = status.PaymentProviderRedirectUrl, PaymentStatus = GetPaymentStatus(status.Status), ResponseObject = "OK" };
            // }
            // return null;
        }
    }

    public static class CashBillProviderExtensions
    {
        public static void RegisterCashbillProvider(this IServiceCollection services)
        {
            services.AddOptions<CashbillServiceOptions>();
            services.ConfigureOptions<CashbillConfigureOptions>();
            services.AddTransient<ICashbillServiceCaller, CashbillServiceCaller>();
            services.AddTransient<ICashbillHttpClient, CashbillHttpClient>();
        }
    }



    public class CashbillConfigureOptions : IConfigureOptions<CashbillServiceOptions>
    {
        private readonly IConfiguration configuration;
        public CashbillConfigureOptions(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void Configure(CashbillServiceOptions options)
        {
            var section = configuration.GetSection(CashbillServiceOptions.ConfigurationKey).Get<CashbillServiceOptions>();

            options.ServiceUrl = section.ServiceUrl;
            options.NegativeReturnUrl = section.NegativeReturnUrl;
            options.ShopId = section.ShopId;
            options.ShopSecretPhrase = section.ShopSecretPhrase;
            options.ReturnUrl = section.ReturnUrl;
        }
    }
}
