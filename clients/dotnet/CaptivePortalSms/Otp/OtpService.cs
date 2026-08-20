using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CaptivePortalSms.Options;
using CaptivePortalSms.Sms;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CaptivePortalSms.Otp;

public sealed partial class OtpService : IOtpService
{
    private readonly ISmsGatewayClient _sms;
    private readonly IMemoryCache _cache;
    private readonly OtpOptions _options;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        ISmsGatewayClient sms,
        IMemoryCache cache,
        IOptions<OtpOptions> options,
        ILogger<OtpService> logger)
    {
        _sms = sms;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    // Cache'te tutulan kod kaydi. Attempts atomik artirilir (lock ile).
    private sealed class OtpEntry
    {
        public required string Code { get; init; }
        public int Attempts;
    }

    public async Task<OtpRequestResult> RequestAsync(string phone, CancellationToken ct = default)
    {
        if (!IsValidPhone(phone))
            return new OtpRequestResult(OtpRequestStatus.InvalidPhone, "Gecersiz telefon formati (E.164 bekleniyor).");

        // Client tarafi cooldown (gateway'in 429'una gelmeden nazik uyari).
        if (_cache.TryGetValue(CooldownKey(phone), out _))
            return new OtpRequestResult(OtpRequestStatus.Cooldown,
                "Cok sik istek. Lutfen bekleyin.", _options.ResendCooldownSeconds);

        var code = GenerateCode(_options.CodeLength);
        var message = BuildMessage(code);

        var result = await _sms.SendAsync(phone, message, ct);
        if (!result.Success)
        {
            _logger.LogWarning("OTP SMS gonderilemedi ({Phone}): {Error}", Mask(phone), result.Error);
            // 429 gateway rate limit -> kullaniciya cooldown gibi yansit.
            if (result.StatusCode == 429)
                return new OtpRequestResult(OtpRequestStatus.Cooldown,
                    "Cok sik istek. Lutfen bekleyin.", _options.ResendCooldownSeconds);
            return new OtpRequestResult(OtpRequestStatus.GatewayError, "Kod gonderilemedi, tekrar deneyin.");
        }

        // Kodu ve cooldown'u cache'e yaz.
        _cache.Set(OtpKey(phone), new OtpEntry { Code = code },
            TimeSpan.FromSeconds(_options.TtlSeconds));
        _cache.Set(CooldownKey(phone), true,
            TimeSpan.FromSeconds(_options.ResendCooldownSeconds));

        _logger.LogInformation("OTP gonderildi ({Phone}).", Mask(phone));
        return new OtpRequestResult(OtpRequestStatus.Sent, "Dogrulama kodu gonderildi.");
    }

    public OtpVerifyResult Verify(string phone, string code)
    {
        if (!IsValidPhone(phone))
            return new OtpVerifyResult(OtpVerifyStatus.InvalidPhone, "Gecersiz telefon formati.");

        if (!_cache.TryGetValue(OtpKey(phone), out OtpEntry? entry) || entry is null)
            return new OtpVerifyResult(OtpVerifyStatus.Expired, "Kodun suresi doldu veya hic uretilmedi.");

        var attempts = Interlocked.Increment(ref entry.Attempts);
        if (attempts > _options.MaxVerifyAttempts)
        {
            _cache.Remove(OtpKey(phone));
            return new OtpVerifyResult(OtpVerifyStatus.TooManyAttempts,
                "Cok fazla hatali deneme. Yeni kod isteyin.");
        }

        if (ConstantTimeEquals(entry.Code, code?.Trim() ?? ""))
        {
            _cache.Remove(OtpKey(phone));
            _logger.LogInformation("OTP dogrulandi ({Phone}).", Mask(phone));
            return new OtpVerifyResult(OtpVerifyStatus.Verified, "Dogrulama basarili.");
        }

        return new OtpVerifyResult(OtpVerifyStatus.Invalid, "Kod hatali.");
    }

    private string BuildMessage(string code)
    {
        var ttlMinutes = Math.Max(1, _options.TtlSeconds / 60);
        var body = _options.MessageTemplate
            .Replace("{code}", code)
            .Replace("{ttl}", ttlMinutes.ToString());
        // Marka etiketi burada (client'ta) ekleniyor; gateway degismez.
        var label = _options.SenderLabel.Trim();
        return string.IsNullOrEmpty(label) ? body : $"{label}: {body}";
    }

    private static string GenerateCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        var n = RandomNumberGenerator.GetInt32(0, max);
        return n.ToString().PadLeft(length, '0');
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static bool IsValidPhone(string phone) => PhoneRegex().IsMatch(phone);

    private static string Mask(string phone) =>
        phone.Length <= 6 ? "****" : $"{phone[..5]}****{phone[^4..]}";

    private static string OtpKey(string phone) => $"otp:{phone}";
    private static string CooldownKey(string phone) => $"otp-cd:{phone}";

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex PhoneRegex();
}
