namespace CaptivePortalSms.Options;

/// <summary>
/// Yasal saklama süresi ayarları. appsettings.json -> "Retention".
/// Süresi dolan kayıtlar periyodik olarak silinir (KVKK saklama sınırı / 5651).
/// </summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Kayıtların saklanacağı gün sayısı. 2 yıl = 730 gün.</summary>
    public int RetentionDays { get; set; } = 730;

    /// <summary>Temizlik görevinin çalışma aralığı (saat).</summary>
    public int RunIntervalHours { get; set; } = 24;
}
