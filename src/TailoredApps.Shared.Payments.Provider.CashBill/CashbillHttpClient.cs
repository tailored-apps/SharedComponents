using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    /// <summary>
    /// Concrete implementation of <see cref="ICashbillHttpClient"/>.
    /// Sends HTTP requests to the CashBill REST API using <see cref="WebRequest"/>
    /// with form-encoded payloads.
    /// </summary>
    public class CashbillHttpClient : ICashbillHttpClient
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of <see cref="CashbillHttpClient"/>.
        /// </summary>
        /// <param name="logger">Logger instance for error reporting.</param>
        public CashbillHttpClient(ILogger<CashbillHttpClient> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<T> MakeFormRequest<T>(string url, string method, FormUrlEncodedContent requestFormData)
        {
            try
            {
                var newUrl = new Uri(url);
                WebRequest request22 = WebRequest.Create(newUrl.ToString());
                request22.Method = method;
                if (method != "GET")
                {
                    byte[] byteArray = Encoding.UTF8.GetBytes(await requestFormData.ReadAsStringAsync());
                    request22.ContentType = "application/x-www-form-urlencoded; charset=UTF8";
                    request22.ContentLength = byteArray.Length;
                    using (Stream dataStream = request22.GetRequestStream())
                    {
                        dataStream.Write(byteArray, 0, byteArray.Length);
                        dataStream.Close();

                        using (WebResponse response22 = request22.GetResponse())
                        {
                            using (var responseStream = response22.GetResponseStream())
                            {
                                using (StreamReader reader22 = new StreamReader(responseStream))
                                {
                                    string responseFromServer = reader22.ReadToEnd();
                                    T payment = JsonSerializer.Deserialize<T>(responseFromServer);
                                    return payment;
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (WebResponse response22 = request22.GetResponse())
                    {
                        using (var responseStream = response22.GetResponseStream())
                        {
                            using (StreamReader reader22 = new StreamReader(responseStream))
                            {
                                string responseFromServer = reader22.ReadToEnd();
                                T payment = JsonSerializer.Deserialize<T>(responseFromServer);
                                return payment;
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message + ex.StackTrace);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task MakeFormRequest(string url, string method, FormUrlEncodedContent requestFormData)
        {
            try
            {
                var newUrl = new Uri(url);
                WebRequest request22 = WebRequest.Create(newUrl.ToString());
                request22.Method = method;
                byte[] byteArray = Encoding.UTF8.GetBytes(await requestFormData.ReadAsStringAsync());
                request22.ContentType = "application/x-www-form-urlencoded; charset=UTF8";
                request22.ContentLength = byteArray.Length;
                using (Stream dataStream = request22.GetRequestStream())
                {
                    if (method != "GET")
                    {
                        dataStream.Write(byteArray, 0, byteArray.Length);
                        dataStream.Close();
                    }
                    using (WebResponse response22 = request22.GetResponse())
                    {
                        using (var responseStream = response22.GetResponseStream())
                        {
                            using (StreamReader reader22 = new StreamReader(responseStream))
                            {
                                string responseFromServer = reader22.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message + ex.StackTrace);
                throw;
            }
        }
    }
}
