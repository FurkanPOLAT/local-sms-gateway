namespace CaptivePortalSms.Options;

/// <summary>
/// OTP davranis ayarlari. appsettings.json -> "Otp".
/// </summary>
public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    /// <summary>Kod uzunlugu (hane).</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Kodun gecerlilik suresi (sn).</summary>
    public int TtlSeconds { get; set; } = 180;

    /// <summary>Bir kod icin izin verilen maksimum yanlis deneme.</summary>
    public int MaxVerifyAttempts { get; set; } = 5;

    /// <summary>Ayni numaraya yeni kod istemeden once beklenmesi gereken sure (sn).</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>SMS metninin basina eklenecek marka etiketi (client tarafi).</summary>
    public string SenderLabel { get; set; } = "SLNMODA";

    /// <summary>
    /// SMS sablonu. {code} ve {ttl} (dakika) yer tutuculari desteklenir.
    /// </summary>
    public string MessageTemplate { get; set; } =
        "Misafir Wi-Fi dogrulama kodunuz: {code}. Kod {ttl} dakika gecerlidir.";
}
