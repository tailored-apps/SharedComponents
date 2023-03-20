using System.Net.Http;
using System.Threading.Tasks;

namespace TailoredApps.Shared.Payments.Provider.CashBill
{
    public interface ICashbillHttpClient
    {
        Task<T> MakeFormRequest<T>(string url, string method, FormUrlEncodedContent requestFormData);
        Task MakeFormRequest(string url, string method, FormUrlEncodedContent requestFormData);
    }
}
