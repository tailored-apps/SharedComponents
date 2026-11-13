namespace TailoredApps.Demo.DataModel.Orders.Responses.Commands;

public class ReceivePaymentNotificationResponse
{
    /// <summary>False when the notification could not be trusted; the gateway is then asked to send it again.</summary>
    public bool Accepted { get; set; }

    /// <summary>Why the notification was not accepted.</summary>
    public string? Rejection { get; set; }

    /// <summary>The text the gateway expects as the answer (Tpay: TRUE, Adyen: [accepted]).</summary>
    public string Acknowledgement { get; set; } = "OK";
}
