namespace CaptivePortalSms.Otp;

/// <summary>
/// OTP uretme, SMS ile gonderme ve dogrulama akisi. Captive Portal bu servisi cagirir.
/// </summary>
public interface IOtpService
{
    Task<OtpRequestResult> RequestAsync(string phone, string? consentVersion,
        string ip, string? userAgent, CancellationToken ct = default);

    Task<OtpVerifyResult> VerifyAsync(string phone, string code, string ip,
        string? deviceMac, CancellationToken ct = default);
}
