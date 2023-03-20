namespace TailoredApps.Shared.Payments
{
    public class PaymentRequest
    {
        public string PaymentProvider { get; set; }
        public string PaymentChannel { get; set; }
        public PaymentModel PaymentModel { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string Surname { get; set; }
        public string Street { get; set; }
        public string House { get; set; }
        public string Flat { get; set; }
        public string PostCode { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string AdditionalData { get; set; }
        public string Referer { get; set; }
    }
}