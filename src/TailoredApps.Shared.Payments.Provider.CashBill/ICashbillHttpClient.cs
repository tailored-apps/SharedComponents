using System.Net.Http;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    /// <summary>
    /// Low-level HTTP client abstraction for communicating with the CashBill REST API.
    /// Enables mocking in unit tests.
    /// </summary>
    public interface ICashbillHttpClient
    {
        /// <summary>
        /// Sends an HTTP request with form-encoded body and deserializes the response to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Expected response model type.</typeparam>
        /// <param name="url">Absolute URL of the CashBill API endpoint.</param>
        /// <param name="method">HTTP method (e.g. "GET", "POST", "PUT").</param>
        /// <param name="requestFormData">Form-encoded content to send with the request; may be <c>null</c> for GET requests.</param>
        /// <returns>Deserialized response of type <typeparamref name="T"/>.</returns>
        Task<T> MakeFormRequest<T>(string url, string method, FormUrlEncodedContent requestFormData);

        /// <summary>
        /// Sends an HTTP request with form-encoded body without deserializing the response.
        /// Used for fire-and-forget calls (e.g. updating return URLs).
        /// </summary>
        /// <param name="url">Absolute URL of the CashBill API endpoint.</param>
        /// <param name="method">HTTP method (e.g. "POST", "PUT").</param>
        /// <param name="requestFormData">Form-encoded content to send with the request.</param>
        Task MakeFormRequest(string url, string method, FormUrlEncodedContent requestFormData);
    }
}
