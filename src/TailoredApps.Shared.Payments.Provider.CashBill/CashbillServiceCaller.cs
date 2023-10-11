using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TailoredApps.Shared.Payments.Provider.CashBill.Models;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    public class CashbillServiceCaller : ICashbillServiceCaller
    {

        private readonly ICashbillHttpClient cashbillCaller;
        private readonly IOptions<CashbillServiceOptions> options;
        public CashbillServiceCaller(ICashbillHttpClient cashbillCaller, IOptions<CashbillServiceOptions> options)
        {
            this.cashbillCaller = cashbillCaller;
            this.options = options;
        }

        private string Hash(string input)
        {
            var enc = Encoding.GetEncoding("UTF-8");

            byte[] buffer = enc.GetBytes(input);
            var sha1 = SHA1.Create();
            var hash = BitConverter.ToString(sha1.ComputeHash(buffer)).Replace("-", "").ToLower();
            return hash;
        }

        public async Task<ICollection<PaymentChannels>> GetPaymentChannels(string currency)
        {
            var shopId = options.Value.ShopId;
            var mainUrl = options.Value.ServiceUrl;

            List<PaymentChannels> paymentChannels = await cashbillCaller.MakeFormRequest<List<PaymentChannels>>(Path.Combine(mainUrl, "paymentchannels", shopId), "GET", null);
            return paymentChannels.Where(x => x.AvailableCurrencies.Any(c => string.Equals(c, currency, StringComparison.InvariantCultureIgnoreCase))).ToList();
        }

        public async Task<PaymentStatus> GeneratePayment(PaymentRequest request)
        {
            var shopId = options.Value.ShopId;
            var secretPhrase = options.Value.ShopSecretPhrase;
            var mainUrl = new Uri(options.Value.ServiceUrl);
            var returnUrl = new Uri(options.Value.ReturnUrl);
            var negativeReturnUrl = options.Value.NegativeReturnUrl;

            var languageCode = "PL";
            var amountString = request.Amount.ToString().Replace(",", ".").Replace(" ", "");

            var sign = Hash(request.Title
                + amountString
                + request.Currency
                + returnUrl
                + request.Description
                + negativeReturnUrl
                + request.AdditionalData
                + request.PaymentChannel
                + languageCode
                + request.Referer
                + request.FirstName
                + request.Surname
                + request.Email
                + request.Country
                + request.City
                + request.PostCode
                + request.Street
                + request.House
                + request.Flat
                + secretPhrase);



            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string> ( "title", request.Title ),
                new KeyValuePair<string,string> ( "amount.value", amountString ),
                new KeyValuePair<string,string> ( "amount.currencyCode", request.Currency ),
                new KeyValuePair<string,string> ( "returnUrl", returnUrl.ToString() ),
                new KeyValuePair<string,string> ( "description", request.Description ),
                new KeyValuePair<string,string> ( "negativeReturnUrl", negativeReturnUrl ),
                new KeyValuePair<string,string> ( "additionalData", request.AdditionalData ),
                new KeyValuePair<string,string> ( "paymentChannel", request.PaymentChannel ),
                new KeyValuePair<string,string> ( "languageCode", languageCode ),
                new KeyValuePair<string,string> ( "referer", request.Referer ),
                new KeyValuePair<string,string> ( "personalData.firstName", request.FirstName ),
                new KeyValuePair<string,string> ( "personalData.surname", request.Surname ),
                new KeyValuePair<string,string> ( "personalData.email", request.Email ),
                new KeyValuePair<string,string> ( "personalData.country", request.Country ),
                new KeyValuePair<string,string> ( "personalData.city", request.City ),
                new KeyValuePair<string,string> ( "personalData.postcode", request.PostCode ),
                new KeyValuePair<string,string> ( "personalData.street", request.Street ),
                new KeyValuePair<string,string> ( "personalData.house", request.House ),
                new KeyValuePair<string,string> ( "personalData.flat", request.Flat ),
                new KeyValuePair<string,string> ( "sign", sign )
            });


            Payment payment = await cashbillCaller.MakeFormRequest<Payment>(new Uri(mainUrl, $"payment/{shopId}").ToString(), "POST", requestContent);
            returnUrl = new Uri($"{returnUrl}/{payment.Id}");

            ///return url
            var signReturn = Hash(payment.Id + returnUrl + negativeReturnUrl + secretPhrase);
            var requestReturnBrowserContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string> ( "returnUrl", returnUrl.ToString() ),
                new KeyValuePair<string,string> ( "negativeReturnUrl", negativeReturnUrl ),
                new KeyValuePair<string,string> ( "sign", signReturn )
            });
            await cashbillCaller.MakeFormRequest(new Uri(mainUrl, $"payment/{shopId}/{payment.Id}").ToString(), "PUT", requestReturnBrowserContent);

            //status 
            var signStatus = Hash(payment.Id + secretPhrase);
            var status = await cashbillCaller.MakeFormRequest<PaymentStatus>(new Uri(mainUrl, $"payment/{shopId}/{payment.Id}?sign={signStatus}").ToString(), "GET", null);

            status.PaymentProviderRedirectUrl = payment.RedirectUrl;
            return status;
        }
        public async Task<PaymentStatus> GetPaymentStatus(string paymentId)
        {
            var shopId = options.Value.ShopId;
            var secretPhrase = options.Value.ShopSecretPhrase;
            var mainUrl = new Uri(options.Value.ServiceUrl);

            var signStatus = Hash(paymentId + secretPhrase);
            var status = await cashbillCaller.MakeFormRequest<PaymentStatus>(new Uri(mainUrl, $"payment/{shopId}/{paymentId}?sign={signStatus}").ToString(), "GET", null);

            return status;
        }
        public async Task<string> GetSignForNotificationService(TransactionStatusChanged transactionStatusChanged)
        {
            return await Task.Run(() =>
             {
                 var secretPhrase = options.Value.ShopSecretPhrase;
                 var toCalc = (transactionStatusChanged.Command + transactionStatusChanged.TransactionId + secretPhrase).Trim();
                 var signStatus = Hash(toCalc);
                 return signStatus;
             });
        }


        public static class PaymentStatusConst
        {
            /// <summary>
            /// Płatność została rozpoczęta, klient nie wybrał jeszcze kanału płatności.
            /// </summary>
            public static readonly string PreStart = "PreStart";
            /// <summary>
            /// Płatość została rozpoczęta, klient nie dokonał jeszcze wpłaty.
            /// </summary>
            public static readonly string Start = "Start";
            /// <summary>
            /// Operator płatności odmówił autoryzacji płatności.
            /// </summary>
            public static readonly string NegativeAuthorization = "NegativeAuthorization";
            /// <summary>
            ///  Klient zrezygnował z dokonania płatności, status jest ostateczny i nie może ulec zmianie.
            /// </summary>
            public static readonly string Abort = "Abort";
            /// <summary>
            /// Operator płatności określił transakcje jako próbę wyłudzenia, jej realizacja zostaje wstrzymana. Status jest ostateczny i nie może ulec zmianie.
            /// </summary>
            public static readonly string Fraud = "Fraud";
            /// <summary>
            /// Transakcja została wstępnie pozytywnie autoryzowana przez operatora płatności, jej ostateczny status zostanie określony w późniejszym okresie.
            /// </summary>
            public static readonly string PositiveAuthorization = "PositiveAuthorization";
            /// <summary>
            /// Operator płatności ostatecznie pozytywnie potwierdził poprawność przeprowadzonej płatności, status jest ostateczny i nie może ulec zmianie.
            /// </summary>
            public static readonly string PositiveFinish = "PositiveFinish";
            /// <summary>
            /// Operator płatności ostatecznie nie potwierdził poprawności przeprowadzonej płatności, status jest ostateczny i nie może ulec zmianie.
            /// </summary>
            public static readonly string NegativeFinish = "NegativeFinish";
            /// <summary>
            /// Czas na wykonanie transakcji , status jest ostateczny i nie może ulec zmianie.
            /// </summary>
            public static readonly string TimeExceeded = "TimeExceeded";
            /// <summary>
            ///  Błąd krytyczny, status jest ostateczny i nie może ulec zmianie.
            /// </summary>
            public static readonly string CriticalError = "CriticalError";

        }
    }
}
