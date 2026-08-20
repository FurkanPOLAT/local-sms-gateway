# Captive Portal SMS Client (.NET 8)

Misafir Wi-Fi Captive Portal'ı için OTP (tek kullanımlık kod) akışı. Kodu üretir,
**Android SMS Gateway** üzerinden gönderir ve doğrular. Gateway "aptal boru"dur;
OTP mantığı, `SLNMODA:` marka etiketi ve mesaj şablonu **bu katmanda** yaşar.

```
[ Tarayici / Captive Portal ]
        │  POST /api/otp/request  { phone }
        ▼
[ Bu servis (.NET 8) ]  --- OTP uret + cache + sablon --->  [ SmsGatewayClient ]
                                                                    │ POST /api/v1/sms/send
                                                                    ▼
                                                            [ Android Gateway :8080 ]
```

## Yapı

| Dosya | Sorumluluk |
| --- | --- |
| `Sms/SmsGatewayClient.cs` | Gateway'e istek (typed HttpClient, retry + backoff, X-API-KEY) |
| `Otp/OtpService.cs` | Kod üret, cache'le, cooldown, doğrula (sabit-zamanlı karşılaştırma) |
| `Program.cs` | DI + `/api/otp/request` ve `/api/otp/verify` endpoint'leri |
| `appsettings.json` | Gateway adresi/anahtarı + OTP ayarları |

## Ayarlar

`appsettings.json` içinde:
- `SmsGateway.BaseUrl` → gateway cihazının statik IP'si (örn. `http://192.168.1.50:8080`)
- `SmsGateway.ApiKey` → **uygulama içindeki X-API-KEY**

> **Güvenlik:** Anahtarı `appsettings.json`'a yazıp commit etmeyin. Geliştirmede user-secrets kullanın:
> ```bash
> dotnet user-secrets init
> dotnet user-secrets set "SmsGateway:ApiKey" "GATEWAY_ANAHTARI"
> ```
> Üretimde ortam değişkeni / secret store'dan gelmelidir.

## Çalıştırma

```bash
cd clients/dotnet/CaptivePortalSms
dotnet run
```

`CaptivePortalSms.http` dosyasındaki istekleri (VS/VS Code REST Client) veya curl ile deneyin:

```bash
# 1) Kod iste (telefona SMS düşer)
curl -X POST http://localhost:5080/api/otp/request -H "Content-Type: application/json" -d "{\"phone\":\"+905321112233\"}"

# 2) Gelen kodu doğrula
curl -X POST http://localhost:5080/api/otp/verify -H "Content-Type: application/json" -d "{\"phone\":\"+905321112233\",\"code\":\"481920\"}"
```

## HTTP durum kodları

| Uç | Durum | Kod |
| --- | --- | --- |
| `/otp/request` | Kod gönderildi | 200 |
| | Geçersiz telefon | 400 |
| | Çok sık istek (cooldown / gateway 429) | 429 |
| | Gateway hatası | 502 |
| `/otp/verify` | Doğru | 200 |
| | Hatalı kod | 400 |
| | Süresi dolmuş | 410 |
| | Çok fazla deneme | 429 |

## Notlar

- **Ölçekleme:** OTP'ler `IMemoryCache`'te (process içi) tutulur. Birden fazla instance / load balancer varsa Redis (`IDistributedCache`) kullanın.
- **Marka etiketi:** `Otp.SenderLabel` boş bırakılırsa etiket eklenmez. Gerçek gönderici numarası SIM'in numarasıdır (Android sınırı).
