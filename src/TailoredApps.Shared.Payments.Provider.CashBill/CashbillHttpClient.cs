using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    public class CashbillHttpClient : ICashbillHttpClient
    {
        private readonly ILogger logger;
        public CashbillHttpClient(ILogger<CashbillHttpClient> logger)
        {
            this.logger = logger;
        }
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
