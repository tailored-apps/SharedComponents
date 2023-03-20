using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace TailoredApps.Shared.Payments
{
    public class TransactionStatusChangePayload
    {
        public string ProviderId { get; set; }
        public object Payload { get; set; }
        public Dictionary<string, StringValues> QueryParameters { get; set; }
    }
}
