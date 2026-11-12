using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using TailoredApps.Shared.Payments.Provider.Adyen;
using Xunit;

namespace TailoredApps.Shared.Payments.Tests;

/// <summary>
/// Standard Adyen webhooks: the signature is in <c>additionalData.hmacSignature</c> of the item and covers
/// eight of its fields joined with a colon.
/// </summary>
public class AdyenNotificationTests
{
    // The example published in Adyen's "Verify HMAC signatures" documentation.
    private const string DocumentedKey = "44782DEF547AAA06C910C43932B1EB0C71FC68D9D0C057550C48EC2ACF6BA056";
    private const string DocumentedSignedData = "7914073381342284::TestMerchant:TestPayment-1407325143704:1130:EUR:AUTHORISATION:true";
    private const string DocumentedSignature = "coqCmt/IZ4E3CzPvMY8zTjQVL5hYJUiBRg8UU+iCWo0=";

    private static AdyenProvider Provider(string hmacKey = DocumentedKey)
    {
        var factory = new Mock<IHttpClientFactory>();
        var options = Options.Create(new AdyenServiceOptions { ApiKey = "key", MerchantAccount = "TestMerchant", NotificationHmacKey = hmacKey });
        return new AdyenProvider(new AdyenServiceCaller(options, factory.Object));
    }

    /// <summary>The documented example as Adyen posts it; each argument replaces one signed field.</summary>
    private static string Notification(
        string pspReference = "7914073381342284",
        string merchantAccountCode = "TestMerchant",
        string merchantReference = "TestPayment-1407325143704",
        string amountValue = "1130",
        string currency = "EUR",
        string eventCode = "AUTHORISATION",
        string success = "\"true\"",
        string signature = DocumentedSignature,
        string extra = "")
        => $$"""
           {
             "live": "false",
             "notificationItems": [
               {
                 "NotificationRequestItem": {
                   "additionalData": { "hmacSignature": "{{signature}}" },
                   "amount": { "currency": "{{currency}}", "value": {{amountValue}} },
                   "eventCode": "{{eventCode}}",
                   "eventDate": "2014-08-06T13:39:03+02:00",
                   "merchantAccountCode": "{{merchantAccountCode}}",
                   "merchantReference": "{{merchantReference}}",
                   "paymentMethod": "visa",
                   "pspReference": "{{pspReference}}",{{extra}}
                   "success": {{success}}
                 }
               }
             ]
           }
           """;

    private static string Hmac(string signedData, string hexKey = DocumentedKey)
        => Convert.ToBase64String(HMACSHA256.HashData(Convert.FromHexString(hexKey), Encoding.UTF8.GetBytes(signedData)));

    private static Task<PaymentWebhookResult> Handle(string body, string hmacKey = DocumentedKey)
        => Provider(hmacKey).HandleWebhookAsync(new PaymentWebhookRequest { Body = body, ContentType = "application/json" });

    [Fact]
    public void When_Documented_Example_Is_Signed_With_The_Hex_Decoded_Key_Should_Match_The_Documented_Signature()
    {
        Assert.Equal(DocumentedSignature, Hmac(DocumentedSignedData));
    }

    [Fact]
    public async Task When_Notification_Is_The_Documented_Example_Should_Accept_It()
    {
        // act
        var result = await Handle(Notification());

        // assert
        var processed = Assert.IsType<PaymentWebhookProcessed>(result);
        Assert.Equal(PaymentStatusEnum.Finished, processed.Payment.PaymentStatus);
        Assert.Equal("TestPayment-1407325143704", processed.Payment.PaymentUniqueId);
        Assert.Equal("[accepted]", processed.Payment.ResponseObject);
    }

    [Fact]
    public async Task When_Hmac_Key_Is_Lower_Case_Hex_Should_Accept_The_Documented_Example()
    {
        Assert.IsType<PaymentWebhookProcessed>(await Handle(Notification(), DocumentedKey.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("pspReference")]
    [InlineData("merchantAccountCode")]
    [InlineData("merchantReference")]
    [InlineData("amountValue")]
    [InlineData("currency")]
    [InlineData("eventCode")]
    [InlineData("success")]
    public async Task When_A_Signed_Field_Is_Changed_Should_Reject_As_InvalidSignature(string field)
    {
        // arrange
        var body = field switch
        {
            "pspReference" => Notification(pspReference: "7914073381342285"),
            "merchantAccountCode" => Notification(merchantAccountCode: "OtherMerchant"),
            "merchantReference" => Notification(merchantReference: "SomeoneElsesOrder"),
            "amountValue" => Notification(amountValue: "1"),
            "currency" => Notification(currency: "PLN"),
            "eventCode" => Notification(eventCode: "REFUND"),
            _ => Notification(success: "\"false\""),
        };

        // act
        var result = await Handle(body);

        // assert
        Assert.Equal(PaymentWebhookRejection.InvalidSignature, Assert.IsType<PaymentWebhookRejected>(result).Rejection);
    }

    [Fact]
    public async Task When_Original_Reference_Is_Added_To_A_Signed_Item_Should_Reject()
    {
        // originalReference is the second signed field; the documented signature was made without one
        var result = await Handle(Notification(extra: "\n        \"originalReference\": \"8313547924770610\","));

        Assert.IsType<PaymentWebhookRejected>(result);
    }

    [Fact]
    public async Task When_Unsigned_Fields_Change_Should_Still_Accept()
    {
        var body = Notification().Replace("\"paymentMethod\": \"visa\"", "\"paymentMethod\": \"mc\", \"reason\": \"1234:7777:12/2030\"", StringComparison.Ordinal);

        Assert.IsType<PaymentWebhookProcessed>(await Handle(body));
    }

    [Fact]
    public async Task When_Signature_Is_Missing_From_Additional_Data_Should_Reject_Even_If_Sent_As_A_Header()
    {
        // arrange
        var body = Notification().Replace("\"additionalData\": { \"hmacSignature\": \"" + DocumentedSignature + "\" },", string.Empty, StringComparison.Ordinal);

        // act
        var result = await Provider().HandleWebhookAsync(new PaymentWebhookRequest
        {
            Body = body,
            Headers = new Dictionary<string, StringValues> { { "HmacSignature", DocumentedSignature } },
        });

        // assert
        Assert.Equal(PaymentWebhookRejection.InvalidSignature, Assert.IsType<PaymentWebhookRejected>(result).Rejection);
    }

    [Fact]
    public async Task When_Signature_Was_Made_Over_The_Whole_Body_Should_Reject()
    {
        // The scheme this library used before: HMAC of the request body.
        var unsigned = Notification(signature: "placeholder");
        var body = unsigned.Replace("placeholder", Hmac(unsigned), StringComparison.Ordinal);

        Assert.IsType<PaymentWebhookRejected>(await Handle(body));
    }

    [Fact]
    public async Task When_Hmac_Key_Is_Another_Merchants_Should_Reject()
    {
        var result = await Handle(Notification(), "5152535455565758595A5B5C5D5E5F605152535455565758595A5B5C5D5E5F60");

        Assert.IsType<PaymentWebhookRejected>(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-hex")]
    public async Task When_Hmac_Key_Is_Unusable_Should_Reject(string hmacKey)
    {
        Assert.IsType<PaymentWebhookRejected>(await Handle(Notification(), hmacKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NOT_JSON")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("""{"notificationItems":[]}""")]
    [InlineData("""{"notificationItems":{}}""")]
    [InlineData("""{"notificationItems":[{"SomethingElse":{}}]}""")]
    [InlineData("""{"notificationItems":["text"]}""")]
    [InlineData("""{"notificationItems":[{"NotificationRequestItem":"text"}]}""")]
    public async Task When_Body_Has_No_Notification_Item_Should_Reject_As_MalformedPayload(string body)
    {
        var result = await Handle(body);

        Assert.Equal(PaymentWebhookRejection.MalformedPayload, Assert.IsType<PaymentWebhookRejected>(result).Rejection);
    }

    [Theory]
    [InlineData("""{"notificationItems":[{"NotificationRequestItem":{"additionalData":"text","eventCode":"AUTHORISATION"}}]}""")]
    [InlineData("""{"notificationItems":[{"NotificationRequestItem":{"additionalData":{"hmacSignature":123},"eventCode":"AUTHORISATION"}}]}""")]
    [InlineData("""{"notificationItems":[{"NotificationRequestItem":{"eventCode":"AUTHORISATION","success":"true"}}]}""")]
    public async Task When_Item_Has_No_Usable_Signature_Should_Reject_As_InvalidSignature(string body)
    {
        var result = await Handle(body);

        Assert.Equal(PaymentWebhookRejection.InvalidSignature, Assert.IsType<PaymentWebhookRejected>(result).Rejection);
    }

    [Fact]
    public async Task When_Item_Is_Verified_Should_Pass_The_Eight_Fields_In_Adyens_Order_To_The_Caller()
    {
        // arrange: a refund, which carries the original payment's reference; success and amount as JSON literals
        const string body = """
            {"notificationItems":[{"NotificationRequestItem":{
              "additionalData":{"hmacSignature":"sig"},
              "amount":{"currency":"PLN","value":2599},
              "eventCode":"REFUND","merchantAccountCode":"Shop","merchantReference":"order:7",
              "originalReference":"PSP-ORIGINAL","pspReference":"PSP-REFUND","success":true}}]}
            """;
        string signedData = null;
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.VerifyNotificationHmac(It.IsAny<string>(), "sig"))
              .Callback<string, string>((data, _) => signedData = data)
              .Returns(true);

        // act
        var result = await new AdyenProvider(caller.Object).HandleWebhookAsync(new PaymentWebhookRequest { Body = body });

        // assert
        Assert.Equal("PSP-REFUND:PSP-ORIGINAL:Shop:order:7:2599:PLN:REFUND:true", signedData);
        Assert.Equal(PaymentStatusEnum.Finished, Assert.IsType<PaymentWebhookProcessed>(result).Payment.PaymentStatus);
    }

    [Theory]
    [InlineData("""{"additionalData":{"hmacSignature":"sig"},"eventCode":"AUTHORISATION","success":false,"amount":"text"}""", ":::::: AUTHORISATION:false")]
    [InlineData("""{"additionalData":{"hmacSignature":"sig"},"eventCode":"REPORT_AVAILABLE","pspReference":null,"success":{}}""", "::::::REPORT_AVAILABLE:")]
    public async Task When_Fields_Are_Absent_Or_Not_Scalar_Should_Sign_Them_As_Empty_Text(string body, string expected)
    {
        // arrange
        string signedData = null;
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.VerifyNotificationHmac(It.IsAny<string>(), "sig"))
              .Callback<string, string>((data, _) => signedData = data)
              .Returns(true);

        // act
        await new AdyenProvider(caller.Object).HandleWebhookAsync(new PaymentWebhookRequest { Body = body });

        // assert
        Assert.Equal(expected.Replace(" ", string.Empty, StringComparison.Ordinal), signedData);
    }

    [Fact]
    public async Task When_Event_Is_Not_Tracked_Should_Ignore_It_With_The_Acknowledgement()
    {
        // arrange
        const string body = """{"notificationItems":[{"NotificationRequestItem":{"additionalData":{"hmacSignature":"sig"},"eventCode":"REPORT_AVAILABLE","pspReference":"report-1","success":"true"}}]}""";
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.VerifyNotificationHmac("report-1::::::REPORT_AVAILABLE:true", "sig")).Returns(true);

        // act
        var result = await new AdyenProvider(caller.Object).HandleWebhookAsync(new PaymentWebhookRequest { Body = body });

        // assert
        var ignored = Assert.IsType<PaymentWebhookIgnored>(result);
        Assert.Equal("report-1", ignored.Payment!.PaymentUniqueId);
        Assert.Equal(AdyenProvider.Acknowledgement, ignored.Payment.ResponseObject);
    }

    [Fact]
    public async Task When_Item_Has_No_References_Should_Return_No_Payment_Id()
    {
        // arrange
        const string body = """{"notificationItems":[{"NotificationRequestItem":{"additionalData":{"hmacSignature":"sig"},"eventCode":"CANCELLATION","success":"true"}}]}""";
        var caller = new Mock<IAdyenServiceCaller>();
        caller.Setup(c => c.VerifyNotificationHmac(It.IsAny<string>(), "sig")).Returns(true);

        // act
        var result = await new AdyenProvider(caller.Object).HandleWebhookAsync(new PaymentWebhookRequest { Body = body });

        // assert
        var processed = Assert.IsType<PaymentWebhookProcessed>(result);
        Assert.Null(processed.Payment.PaymentUniqueId);
        Assert.Equal(PaymentStatusEnum.Rejected, processed.Payment.PaymentStatus);
    }

    [Fact]
    public async Task When_Called_Through_The_Legacy_Method_Should_Verify_The_Same_Way()
    {
        var accepted = await Provider().TransactionStatusChange(new TransactionStatusChangePayload { Payload = Notification() });
        var rejected = await Provider().TransactionStatusChange(new TransactionStatusChangePayload { Payload = Notification(amountValue: "1") });
        var missing = await Provider().TransactionStatusChange(new TransactionStatusChangePayload());

        Assert.Equal(PaymentStatusEnum.Finished, accepted.PaymentStatus);
        Assert.Equal("TestPayment-1407325143704", accepted.PaymentUniqueId);
        Assert.Equal(PaymentStatusEnum.Rejected, rejected.PaymentStatus);
        Assert.Equal(PaymentStatusEnum.Rejected, missing.PaymentStatus);
    }
}
