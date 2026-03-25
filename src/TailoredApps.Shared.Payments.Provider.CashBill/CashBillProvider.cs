using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TailoredApps.Shared.Payments;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;
using static TailoredApps.Shared.Payments.Provider.CashBill.CashbillServiceCaller;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    /// <summary>
    /// Payment provider implementation for CashBill.
    /// Supports both polling-based status checks and back-channel webhook notifications
    /// via <see cref="IWebhookPaymentProvider"/>.
    /// </summary>
    public class CashBillProvider : IPaymentProvider, IWebhookPaymentProvider
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

        /// <summary>
        /// Returns available payment channels from CashBill filtered by the requested currency.
        /// </summary>
        /// <param name="currency">ISO 4217 currency code (e.g. "PLN").</param>
        /// <returns>Collection of <see cref="PaymentChannel"/> objects supported for the given currency.</returns>
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

        /// <summary>
        /// Initiates a new CashBill payment transaction and returns the provider redirect URL.
        /// </summary>
        /// <param name="request">Payment details including amount, currency, and payer data.</param>
        /// <returns>
        /// <see cref="PaymentResponse"/> with <see cref="PaymentResponse.RedirectUrl"/> pointing to
        /// the CashBill hosted payment page and the initial payment status.
        /// </returns>
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


        /// <summary>
        /// Retrieves the current status of a CashBill payment by its transaction ID.
        /// </summary>
        /// <param name="paymentId">CashBill transaction identifier.</param>
        /// <returns>Current <see cref="PaymentResponse"/> including the normalised payment status.</returns>
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

        /// <summary>
        /// Processes a legacy back-channel status-change notification from CashBill.
        /// Reads <c>cmd</c>, <c>args</c> (transaction ID) and <c>sign</c> from the query parameters,
        /// then fetches the current payment status from the CashBill API.
        /// </summary>
        /// <param name="payload">Payload containing query parameters sent by CashBill.</param>
        /// <returns>Resolved <see cref="PaymentResponse"/> with the current payment status.</returns>
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

        // ─── IWebhookPaymentProvider ─────────────────────────────────────────

        /// <summary>
        /// Handles an incoming CashBill back-channel HTTP notification.
        /// </summary>
        /// <remarks>
        /// CashBill sends a GET/POST with query-string parameters:
        /// <c>cmd</c> (event type), <c>args</c> (transaction ID), <c>sign</c> (MD5 signature).<br/>
        /// The method:
        /// <list type="number">
        ///   <item>Validates that <c>args</c> (transaction ID) is present.</item>
        ///   <item>Verifies the MD5 signature via <c>GetSignForNotificationService</c>.</item>
        ///   <item>Fetches the current payment status from CashBill API.</item>
        ///   <item>Returns a normalised <see cref="PaymentWebhookResult"/>.</item>
        /// </list>
        /// </remarks>
        /// <param name="request">Unified HTTP webhook request containing query parameters.</param>
        public async Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookRequest request)
        {
            var cmd           = request.Query.TryGetValue("cmd",  out var c) ? c.ToString() : string.Empty;
            var transactionId = request.Query.TryGetValue("args", out var a) ? a.ToString() : string.Empty;
            var sign          = request.Query.TryGetValue("sign", out var s) ? s.ToString() : string.Empty;

            if (string.IsNullOrEmpty(transactionId))
                return PaymentWebhookResult.Fail("Missing transactionId (args) in query string.");

            // Verify MD5 signature: MD5(cmd + args + shopSecretPhrase)
            var notification  = new TransactionStatusChanged { Command = cmd, TransactionId = transactionId, Sign = sign };
            var expectedSign  = await cashbillService.GetSignForNotificationService(notification);
            if (!string.Equals(expectedSign, sign, StringComparison.OrdinalIgnoreCase))
                return PaymentWebhookResult.Fail($"Invalid signature. expected={expectedSign} got={sign}");

            // Signature valid — poll the API for the actual payment status
            var statusResponse = await GetStatus(transactionId);
            return PaymentWebhookResult.Ok(statusResponse);
        }
    }

    /// <summary>DI extension methods for the CashBill payment provider.</summary>
    public static class CashBillProviderExtensions
    {
        /// <summary>
        /// Registers the CashBill provider and its dependencies.
        /// The provider is exposed as both <see cref="IPaymentProvider"/>
        /// (used by the <c>PaymentService</c> aggregator)
        /// and <see cref="IWebhookPaymentProvider"/>
        /// (used for back-channel webhook dispatch).
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        public static void RegisterCashbillProvider(this IServiceCollection services)
        {
            services.AddOptions<CashbillServiceOptions>();
            services.ConfigureOptions<CashbillConfigureOptions>();
            services.AddTransient<ICashbillServiceCaller, CashbillServiceCaller>();
            services.AddTransient<ICashbillHttpClient, CashbillHttpClient>();

            // Register as both IPaymentProvider and IWebhookPaymentProvider
            services.AddTransient<CashBillProvider>();
            services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<CashBillProvider>());
            services.AddTransient<IWebhookPaymentProvider>(sp => sp.GetRequiredService<CashBillProvider>());
        }
    }



    /// <summary>
    /// Binds <see cref="CashbillServiceOptions"/> from the application configuration
    /// at the section defined by <see cref="CashbillServiceOptions.ConfigurationKey"/>.
    /// </summary>
    public class CashbillConfigureOptions : IConfigureOptions<CashbillServiceOptions>
    {
        private readonly IConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of <see cref="CashbillConfigureOptions"/>.
        /// </summary>
        /// <param name="configuration">Application configuration instance.</param>
        public CashbillConfigureOptions(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <inheritdoc/>
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
