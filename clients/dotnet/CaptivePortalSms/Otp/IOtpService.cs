namespace CaptivePortalSms.Otp;

/// <summary>
/// OTP uretme, SMS ile gonderme ve dogrulama akisi. Captive Portal bu servisi cagirir.
/// </summary>
public interface IOtpService
{
    Task<OtpRequestResult> RequestAsync(string phone, CancellationToken ct = default);

    OtpVerifyResult Verify(string phone, string code);
}
