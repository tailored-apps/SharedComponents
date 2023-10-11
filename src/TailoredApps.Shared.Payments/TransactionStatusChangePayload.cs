using Microsoft.Extensions.Primitives;
using System.Collections.Generic;

namespace TailoredApps.Shared.Payments
{
    public class TransactionStatusChangePayload
    {
        public string ProviderId { get; set; }
        public object Payload { get; set; }
        public Dictionary<string, StringValues> QueryParameters { get; set; }
    }
}
