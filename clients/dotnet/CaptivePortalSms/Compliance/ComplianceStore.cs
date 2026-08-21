using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CaptivePortalSms.Compliance;

/// <summary>
/// Hash zincirli uyumluluk kayıt deposu. Ekleme işlemleri bir semafor ile
/// serileştirilir; böylece zincir (Sequence + PrevHash) tutarlı kalır.
/// Singleton olarak kaydedilir, DbContext'i factory ile üretir.
/// </summary>
public sealed class ComplianceStore : IComplianceStore
{
    private readonly IDbContextFactory<ComplianceDbContext> _factory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const string Genesis = "GENESIS";

    public ComplianceStore(IDbContextFactory<ComplianceDbContext> factory) => _factory = factory;

    public async Task RecordConsentAsync(string phone, string version, string policyHash,
        string ip, string? userAgent, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Consents.Add(new ConsentRecord
        {
            Phone = phone,
            ConsentVersion = version,
            PolicyHash = policyHash,
            IpAddress = ip,
            UserAgent = userAgent,
            CreatedUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<AccessLog> RecordAccessAsync(string phone, string? deviceMac, string ip,
        DateTime sessionStartUtc, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var last = await db.AccessLogs
                .OrderByDescending(x => x.Sequence)
                .FirstOrDefaultAsync(ct);

            var sequence = (last?.Sequence ?? 0) + 1;
            var prevHash = last?.Hash ?? Genesis;

            var entry = new AccessLog
            {
                Sequence = sequence,
                Phone = phone,
                DeviceMac = deviceMac,
                IpAddress = ip,
                OtpVerified = true,
                SessionStartUtc = sessionStartUtc,
                PrevHash = prevHash
            };
            entry.Hash = ComputeHash(entry);

            db.AccessLogs.Add(entry);
            await db.SaveChangesAsync(ct);
            return entry;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChainVerification> VerifyChainAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var all = await db.AccessLogs.OrderBy(x => x.Sequence).ToListAsync(ct);

        var prev = Genesis;
        foreach (var e in all)
        {
            if (e.PrevHash != prev || e.Hash != ComputeHash(e))
                return new ChainVerification(false, e.Sequence, all.Count);
            prev = e.Hash;
        }
        return new ChainVerification(true, null, all.Count);
    }

    /// <summary>Kaydın kanonik metnini SHA-256 ile özetler. Alan sırası sabittir.</summary>
    private static string ComputeHash(AccessLog e)
    {
        var canonical = string.Join("|",
            e.Sequence.ToString(CultureInfo.InvariantCulture),
            e.Phone,
            e.DeviceMac ?? "",
            e.IpAddress,
            e.OtpVerified ? "1" : "0",
            e.SessionStartUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            e.PrevHash);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
