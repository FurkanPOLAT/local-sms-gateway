using Microsoft.EntityFrameworkCore;

namespace CaptivePortalSms.Compliance;

/// <summary>
/// Uyumluluk (KVKK rıza + 5651 erişim) kayıtları için EF Core / SQLite bağlamı.
/// </summary>
public sealed class ComplianceDbContext : DbContext
{
    public ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : base(options) { }

    public DbSet<ConsentRecord> Consents => Set<ConsentRecord>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ConsentRecord>(e =>
        {
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.CreatedUtc);
        });

        b.Entity<AccessLog>(e =>
        {
            e.HasIndex(x => x.Sequence).IsUnique();
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.SessionStartUtc);
        });
    }
}
