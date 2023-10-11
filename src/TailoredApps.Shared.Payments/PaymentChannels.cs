using System.Collections.Generic;

namespace TailoredApps.Shared.Payments
{

    public class PaymentChannel
    {
        public string Id { get; set; }
        public PaymentModel PaymentModel { get; set; }
        public List<string> AvailableCurrencies { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
    }
}
