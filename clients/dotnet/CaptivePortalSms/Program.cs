using CaptivePortalSms.Options;
using CaptivePortalSms.Otp;
using CaptivePortalSms.Sms;
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

var app = builder.Build();

// wwwroot/index.html (Captive Portal ekrani) ayni origin'den sunulur -> CORS yok.
app.UseDefaultFiles();
app.UseStaticFiles();

// ---- Endpoint'ler ----

// Kod iste: OTP uret + SMS gonder.
app.MapPost("/api/otp/request", async (OtpRequestDto dto, IOtpService otp, CancellationToken ct) =>
{
    var r = await otp.RequestAsync(dto.Phone, ct);
    return r.Status switch
    {
        OtpRequestStatus.Sent         => Results.Ok(new { success = true, message = r.Message }),
        OtpRequestStatus.InvalidPhone => Results.BadRequest(new { success = false, message = r.Message }),
        OtpRequestStatus.Cooldown     => Results.Json(
            new { success = false, message = r.Message, retryAfter = r.RetryAfterSeconds },
            statusCode: StatusCodes.Status429TooManyRequests),
        _                             => Results.Json(
            new { success = false, message = r.Message },
            statusCode: StatusCodes.Status502BadGateway),
    };
});

// Kod dogrula.
app.MapPost("/api/otp/verify", (OtpVerifyDto dto, IOtpService otp) =>
{
    var r = otp.Verify(dto.Phone, dto.Code);
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

app.Run();
