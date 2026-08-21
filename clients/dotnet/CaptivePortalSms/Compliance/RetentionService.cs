using CaptivePortalSms.Options;
using Microsoft.Extensions.Options;

namespace CaptivePortalSms.Compliance;

/// <summary>
/// Süresi dolan uyumluluk kayıtlarını periyodik olarak silen arka plan görevi.
/// Uygulama açılışında bir kez, sonra RunIntervalHours aralıklarıyla çalışır.
/// </summary>
public sealed class RetentionService : BackgroundService
{
    private readonly IComplianceStore _store;
    private readonly RetentionOptions _opt;
    private readonly ILogger<RetentionService> _log;

    public RetentionService(IComplianceStore store, IOptions<RetentionOptions> opt, ILogger<RetentionService> log)
    {
        _store = store;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = await _store.PurgeExpiredAsync(_opt.RetentionDays, stoppingToken);
                if (removed > 0)
                    _log.LogInformation("Retention: {N} suresi dolmus kayit silindi ({Days} gun).",
                        removed, _opt.RetentionDays);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Retention temizligi basarisiz oldu.");
            }

            try { await Task.Delay(TimeSpan.FromHours(_opt.RunIntervalHours), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
