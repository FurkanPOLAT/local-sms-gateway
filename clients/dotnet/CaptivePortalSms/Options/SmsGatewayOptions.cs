namespace CaptivePortalSms.Options;

/// <summary>
/// Android SMS Gateway baglanti ayarlari. appsettings.json -> "SmsGateway".
/// </summary>
public sealed class SmsGatewayOptions
{
    public const string SectionName = "SmsGateway";

    /// <summary>Ornek: http://192.168.1.50:8080 (gateway cihazinin statik IP'si)</summary>
    public string BaseUrl { get; set; } = "http://192.168.1.50:8080";

    /// <summary>Gateway ekranindaki X-API-KEY. Uretimde secret store'dan gelmeli.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Istek zaman asimi (sn).</summary>
    public int TimeoutSeconds { get; set; } = 12;

    /// <summary>Gecici hatalarda (5xx/aginda) toplam deneme sayisi.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Ilk retry gecikmesi (ms). Her denemede ustel artar.</summary>
    public int RetryBaseDelayMs { get; set; } = 400;
}
