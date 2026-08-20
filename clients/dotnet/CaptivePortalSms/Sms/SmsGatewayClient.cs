using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CaptivePortalSms.Options;
using Microsoft.Extensions.Options;

namespace CaptivePortalSms.Sms;

/// <summary>
/// Typed HttpClient tabanli gateway istemcisi. Gecici hatalarda (5xx / ag / timeout)
/// ustel backoff ile yeniden dener; 4xx (401/400/429) kesin kabul edilir, denenmez.
/// X-API-KEY basligi DI'da (Program.cs) DefaultRequestHeaders'e eklenir.
/// </summary>
public sealed class SmsGatewayClient : ISmsGatewayClient
{
    private readonly HttpClient _http;
    private readonly SmsGatewayOptions _options;
    private readonly ILogger<SmsGatewayClient> _logger;

    public SmsGatewayClient(
        HttpClient http,
        IOptions<SmsGatewayOptions> options,
        ILogger<SmsGatewayClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SmsSendResult> SendAsync(string phone, string message, CancellationToken ct = default)
    {
        var payload = new SmsSendRequest(phone, message);
        // NOT: JSON'u string'e cevirip StringContent ile gonderiyoruz. Boylece
        // Content-Length ayarlanir; PostAsJsonAsync'in chunked gonderimi NanoHTTPD
        // tarafindan cozulemedigi icin (400) bu yol tercih edildi.
        var jsonBody = JsonSerializer.Serialize(payload);
        var attempts = Math.Max(1, _options.MaxRetries + 1);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync("/api/v1/sms/send", content, ct);
                var status = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SMS gonderildi (HTTP {Status}).", status);
                    return SmsSendResult.Ok(status);
                }

                // 4xx -> kesin hata, tekrar deneme.
                if (status is >= 400 and < 500)
                {
                    var body = await SafeReadAsync(response, ct);
                    _logger.LogWarning("SMS reddedildi (HTTP {Status}): {Body}", status, body);
                    return SmsSendResult.Fail(status, body);
                }

                // 5xx -> gecici, retry hakki varsa dene.
                _logger.LogWarning("Gateway 5xx (HTTP {Status}), deneme {Attempt}/{Total}.",
                    status, attempt, attempts);
                if (attempt == attempts)
                    return SmsSendResult.Fail(status, "Gateway sunucu hatasi (5xx).");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Ag hatasi veya timeout -> gecici.
                _logger.LogWarning(ex, "Gateway'e ulasilmadi, deneme {Attempt}/{Total}.", attempt, attempts);
                if (attempt == attempts)
                    return SmsSendResult.Fail(0, $"Gateway'e ulasilamadi: {ex.Message}");
            }

            await Task.Delay(BackoffDelay(attempt), ct);
        }

        return SmsSendResult.Fail(0, "Bilinmeyen hata.");
    }

    private TimeSpan BackoffDelay(int attempt)
    {
        // Ustel: base * 2^(attempt-1).  400ms -> 800ms -> 1600ms ...
        var ms = _options.RetryBaseDelayMs * Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(ms);
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var dto = await response.Content.ReadFromJsonAsync<SmsGatewayResponse>(ct);
            return dto?.Message ?? response.ReasonPhrase ?? "Hata";
        }
        catch
        {
            return response.ReasonPhrase ?? "Hata";
        }
    }
}
