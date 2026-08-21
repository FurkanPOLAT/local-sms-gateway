namespace CaptivePortalSms.Options;

/// <summary>
/// RADIUS dinleyici ayarları. FortiGate captive portal, kullanıcının girdiği
/// telefon (User-Name) + OTP (User-Password) bilgisini bu sunucuya sorar; OTP
/// doğruysa Access-Accept döneriz ve FortiGate erişimi açar.
///
/// SharedSecret boş ise RADIUS dinleyici BAŞLATILMAZ. Gerçek değer env'de tutulur.
/// </summary>
public sealed class RadiusOptions
{
    public const string SectionName = "Radius";

    public int Port { get; set; } = 1812;

    /// <summary>FortiGate'teki RADIUS server tanımıyla BİREBİR aynı olmalı.</summary>
    public string SharedSecret { get; set; } = "";
}
