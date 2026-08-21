namespace CaptivePortalSms.Compliance;

/// <summary>
/// KVKK açık rıza kaydı. Kullanıcının OTP'den ÖNCE onayladığı aydınlatma metninin
/// hangi sürümünü, ne zaman, hangi IP'den onayladığını kanıtlar.
/// </summary>
public sealed class ConsentRecord
{
    public long Id { get; set; }
    public string Phone { get; set; } = "";
    public string ConsentVersion { get; set; } = "";   // örn. "1.0"
    public string PolicyHash { get; set; } = "";        // gösterilen metnin SHA-256'si
    public string IpAddress { get; set; } = "";
    public string? UserAgent { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// 5651 kimlik/erişim kaydı. Kim (numara), hangi cihaz (MAC), hangi IP, ne zaman
/// doğrulandı. Hash zinciri ile değiştirilemezdir: her kayıt bir öncekinin
/// özetini (PrevHash) içerir; herhangi bir kayıt sonradan değişirse zincir kırılır.
/// </summary>
public sealed class AccessLog
{
    public long Id { get; set; }
    public long Sequence { get; set; }                  // monotonik sıra no
    public string Phone { get; set; } = "";
    public string? DeviceMac { get; set; }
    public string IpAddress { get; set; } = "";
    public bool OtpVerified { get; set; }
    public DateTime SessionStartUtc { get; set; }
    public DateTime? SessionEndUtc { get; set; }
    public string PrevHash { get; set; } = "";
    public string Hash { get; set; } = "";
}
