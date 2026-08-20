using System.Text.Json.Serialization;

namespace CaptivePortalSms.Sms;

/// <summary>Gateway'e gonderilen istek govdesi (POST /api/v1/sms/send).</summary>
public sealed record SmsSendRequest(
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("message")] string Message);

/// <summary>Gateway'in dondurdugu standart yanit zarfi.</summary>
public sealed record SmsGatewayResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("timestamp")] string? Timestamp);

/// <summary>SmsGatewayClient'in cagirana dondurdugu sonuc.</summary>
public sealed record SmsSendResult(bool Success, int StatusCode, string? Error)
{
    public static SmsSendResult Ok(int status) => new(true, status, null);
    public static SmsSendResult Fail(int status, string error) => new(false, status, error);
}
