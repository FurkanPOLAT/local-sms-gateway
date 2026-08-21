using System.Security.Cryptography;
using System.Text;
using CaptivePortalSms.Compliance;
using CaptivePortalSms.Options;
using CaptivePortalSms.Otp;
using CaptivePortalSms.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ---- Yapilandirma (Options pattern) ----
builder.Services.Configure<SmsGatewayOptions>(
    builder.Configuration.GetSection(SmsGatewayOptions.SectionName));
builder.Services.Configure<OtpOptions>(
    builder.Configuration.GetSection(OtpOptions.SectionName));

// ---- Bagimliliklar ----
builder.Services.AddMemoryCache();

// Typed HttpClient: base adres, timeout ve X-API-KEY basligi burada bir kez ayarlanir.
builder.Services.AddHttpClient<ISmsGatewayClient, SmsGatewayClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<SmsGatewayOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(opt.ApiKey))
        client.DefaultRequestHeaders.Add("X-API-KEY", opt.ApiKey);
});

builder.Services.AddScoped<IOtpService, OtpService>();

// Uyumluluk veritabani (KVKK riza + 5651 erisim logu, hash zincirli).
var complianceConn = builder.Configuration.GetConnectionString("ComplianceDb")
    ?? "Data Source=compliance.db";
builder.Services.AddDbContextFactory<ComplianceDbContext>(o => o.UseSqlite(complianceConn));
builder.Services.AddSingleton<IComplianceStore, ComplianceStore>();

// Aydinlatma metni saglayicisi (dosyadan yukler, surum + hash hesaplar).
builder.Services.AddSingleton<ConsentPolicyProvider>();

// Yasal saklama süresi + periyodik temizlik görevi.
builder.Services.Configure<RetentionOptions>(
    builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddHostedService<RetentionService>();

var app = builder.Build();

// Veritabani semasini olustur (yoksa). Ileride migration'a gecilebilir.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ComplianceDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
}

// wwwroot/index.html (Captive Portal ekrani) ayni origin'den sunulur -> CORS yok.
app.UseDefaultFiles();
app.UseStaticFiles();

// ---- Endpoint'ler ----

// Aydinlatma metni: portal OTP'den once bunu gosterir.
app.MapGet("/api/consent/policy", (ConsentPolicyProvider p) =>
    Results.Ok(new { version = p.Version, text = p.Text }));

// Kod iste: KVKK onayini kaydet + OTP uret + SMS gonder.
app.MapPost("/api/otp/request", async (OtpRequestDto dto, IOtpService otp, HttpContext http, CancellationToken ct) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "";
    var ua = http.Request.Headers.UserAgent.ToString();
    var r = await otp.RequestAsync(dto.Phone, dto.ConsentVersion, ip, ua, ct);
    return r.Status switch
    {
        OtpRequestStatus.Sent            => Results.Ok(new { success = true, message = r.Message }),
        OtpRequestStatus.InvalidPhone    => Results.BadRequest(new { success = false, message = r.Message }),
        OtpRequestStatus.ConsentRequired => Results.BadRequest(new { success = false, message = r.Message }),
        OtpRequestStatus.Cooldown        => Results.Json(
            new { success = false, message = r.Message, retryAfter = r.RetryAfterSeconds },
            statusCode: StatusCodes.Status429TooManyRequests),
        _                                => Results.Json(
            new { success = false, message = r.Message },
            statusCode: StatusCodes.Status502BadGateway),
    };
});

// Kod dogrula: basarili olursa 5651 erisim kaydi (hash zincirli) yazilir.
app.MapPost("/api/otp/verify", async (OtpVerifyDto dto, IOtpService otp, HttpContext http, CancellationToken ct) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "";
    // Cihaz MAC'i su an yok; FortiGate entegrasyonunda (portal yonlendirmesi) eklenecek.
    var r = await otp.VerifyAsync(dto.Phone, dto.Code, ip, null, ct);
    return r.Status switch
    {
        OtpVerifyStatus.Verified        => Results.Ok(new { success = true, message = r.Message }),
        OtpVerifyStatus.TooManyAttempts => Results.Json(
            new { success = false, message = r.Message },
            statusCode: StatusCodes.Status429TooManyRequests),
        OtpVerifyStatus.Expired         => Results.Json(
            new { success = false, message = r.Message },
            statusCode: StatusCodes.Status410Gone),
        _                               => Results.BadRequest(new { success = false, message = r.Message }),
    };
});

// Basit saglik ucu.
app.MapGet("/health", () => Results.Ok(new { status = "up" }));

// ---- Yonetim uclari (yasal talep icin) — X-ADMIN-KEY ile korunur ----
var adminKey = builder.Configuration["Admin:ApiKey"] ?? "";

bool Authorized(HttpContext http)
{
    if (string.IsNullOrEmpty(adminKey)) return false; // anahtar tanimli degilse tumu kapali
    var provided = http.Request.Headers["X-ADMIN-KEY"].ToString();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(adminKey));
}

// Zincir butunlugu dogrulama (tampering var mi?).
app.MapGet("/api/admin/chain/verify", async (HttpContext http, IComplianceStore store, CancellationToken ct) =>
    !Authorized(http) ? Results.Unauthorized() : Results.Ok(await store.VerifyChainAsync(ct)));

// Erisim kayitlari (numara/tarih araligina gore) — yasal dISa aktarim.
app.MapGet("/api/admin/access", async (HttpContext http, IComplianceStore store,
        string? phone, DateTime? from, DateTime? to, CancellationToken ct) =>
    !Authorized(http) ? Results.Unauthorized()
        : Results.Ok(await store.QueryAccessAsync(phone, from, to, ct)));

// Riza kayitlari (numaraya gore).
app.MapGet("/api/admin/consent", async (HttpContext http, IComplianceStore store,
        string? phone, CancellationToken ct) =>
    !Authorized(http) ? Results.Unauthorized()
        : Results.Ok(await store.QueryConsentAsync(phone, ct)));

app.Run();
