namespace CaptivePortalSms.Options;

/// <summary>
/// FortiGate dış (external) captive portal tamamlama ayarları.
/// OTP başarılı olunca portal, tarayıcıyı FortiGate'in "post" (fgtauth) adresine
/// magic + bu kullanıcı adı/şifre ile POST eder; FortiGate oturumu yetkilendirir.
///
/// AuthUser/AuthPassword, FortiGate'te captive portal kullanıcı grubunda tanımlı
/// bir yerel kullanıcı olmalıdır. Boş bırakılırsa FortiGate yetkilendirmesi yapılmaz
/// (portal tek başına "başarılı" ekranı gösterir — standalone/test modu).
/// </summary>
public sealed class FortigateOptions
{
    public const string SectionName = "Fortigate";

    public string AuthUser { get; set; } = "";
    public string AuthPassword { get; set; } = "";

    /// <summary>Giriş sonrası kullanıcının yönlendirileceği adres (4Tredir).</summary>
    public string PostLoginUrl { get; set; } = "https://slnmoda.com.tr";
}
