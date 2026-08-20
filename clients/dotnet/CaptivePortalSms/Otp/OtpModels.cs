using System.Text.Json.Serialization;

namespace CaptivePortalSms.Otp;

/// <summary>POST /api/otp/request govdesi.</summary>
public sealed record OtpRequestDto(
    [property: JsonPropertyName("phone")] string Phone);

/// <summary>POST /api/otp/verify govdesi.</summary>
public sealed record OtpVerifyDto(
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("code")] string Code);

public enum OtpRequestStatus { Sent, InvalidPhone, Cooldown, GatewayError }

public enum OtpVerifyStatus { Verified, Invalid, Expired, TooManyAttempts, InvalidPhone }

public sealed record OtpRequestResult(OtpRequestStatus Status, string Message, int? RetryAfterSeconds = null);

public sealed record OtpVerifyResult(OtpVerifyStatus Status, string Message);
