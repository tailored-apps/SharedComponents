namespace TailoredApps.Shared.Payments
{
    public class PaymentResponse
    {
        public string RedirectUrl { get; set; }
        public string PaymentUniqueId { get; set; }
        public PaymentStatusEnum PaymentStatus { get; set; }
        public object ResponseObject { get; set; }
    }
}