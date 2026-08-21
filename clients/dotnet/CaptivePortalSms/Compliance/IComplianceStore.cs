namespace CaptivePortalSms.Compliance;

public sealed record ChainVerification(bool Valid, long? FirstBrokenSequence, long TotalRecords);

/// <summary>
/// KVKK rıza ve 5651 erişim kayıtlarını değiştirilemez (hash zincirli) biçimde
/// yazan ve sorgulayan katman. Yasal talep geldiğinde kanıt üretir.
/// </summary>
public interface IComplianceStore
{
    Task RecordConsentAsync(string phone, string version, string policyHash,
        string ip, string? userAgent, CancellationToken ct = default);

    /// <summary>OTP doğrulandığında çağrılır; zincire yeni bir erişim kaydı ekler.</summary>
    Task<AccessLog> RecordAccessAsync(string phone, string? deviceMac, string ip,
        DateTime sessionStartUtc, CancellationToken ct = default);

    /// <summary>Tüm zinciri baştan hesaplayıp bütünlüğü doğrular (tampering tespiti).</summary>
    Task<ChainVerification> VerifyChainAsync(CancellationToken ct = default);
}
