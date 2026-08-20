# Local GSM SMS Gateway (In-House Service)

Yerel ağda çalışan, Captive Portal (Misafir Wi-Fi) ve iç sistemlerden gelen HTTP POST isteklerini fiziksel SIM kart üzerinden SMS'e dönüştüren hafif ve güvenli yerel SMS Gateway servisi.

> **Uygulama:** Native Android (Kotlin) · NanoHTTPD · Foreground Service · Room (SQLite) · kotlinx.serialization

---

## 1. Mimari ve Çalışma Mantığı

```
[ .NET Web API / Captive Portal ]
        │
        │  HTTP POST (JSON + API Key Header)
        ▼
[ Android / Cross-Platform Gateway ] (IP: 192.168.x.x:8080)
        │
        │  Android SmsManager / GSM API
        ▼
[ Fiziksel SIM Kart / Şirket Hattı ]
        │
        │  GSM Şebekesi
        ▼
[ Misafir Kullanıcı Telefonu (OTP Kod) ]
```

---

## 2. Temel Fonksiyonel Gereksinimler

- **Yerel Web Sunucusu:** Cihaz üzerinde `8080` portunu dinleyen hafif bir HTTP API (NanoHTTPD).
- **Foreground Service (Ön Plan Servisi):** İşletim sisteminin enerji tasarrufu nedeniyle servisi uyutmasını/kapatmasını engellemek için kalıcı bildirim çubuğu servisi.
- **Header Tabanlı Kimlik Doğrulama:** Yalnızca `X-API-KEY` başlığı doğru olan isteklerin işlenmesi (sabit-zamanlı karşılaştırma).
- **GSM SMS Gönderimi:** İşletim sisteminin `SmsManager`'ı üzerinden tek yönlü SMS gönderimi (160+ karakter otomatik multipart).
- **Hafif SQLite Log:** Gönderilen SMS'lerin durum kodları (Success/Failed) ve zaman damgalarının yerel, **maskelenmiş** kaydı.

---

## 3. API Spesifikasyonu

### Endpoint: SMS Gönder

- **Method:** `POST`
- **Path:** `/api/v1/sms/send`
- **Headers:**
  - `Content-Type: application/json`
  - `X-API-KEY: <GIZLI_SERVIS_ANAHTARI>`

#### İstek Gövdesi (Request Body)

```json
{
  "phone": "+905321112233",
  "message": "Misafir Wi-Fi dogrulama kodunuz: 481920"
}
```

#### Başarılı Yanıt (200 OK)

```json
{
  "success": true,
  "message": "SMS gonderim kuyruguna alindi.",
  "timestamp": "2026-08-20T17:15:00Z"
}
```

#### Hata Yanıtları

* **400 Bad Request:** Hatalı telefon formatı veya boş mesaj metni.
* **401 Unauthorized:** Geçersiz veya eksik API Anahtarı.
* **429 Too Many Requests:** Rate limit (aynı numaraya çok sık istek).
* **500 Internal Server Error:** GSM modülü veya SIM kart erişim hatası.

### Endpoint: Sağlık Kontrolü

- **Method:** `GET` · **Path:** `/api/v1/health` · Kimlik doğrulaması yok. Monitoring/uptime kontrolü için.

---

## 4. Güvenlik ve ISO 27001 Uyumluluk Standartları

| Güvenlik Kuralı | Açıklama | Bu Projede |
| --- | --- | --- |
| **Ağ İzolasyonu** | Cihaz yalnızca sunucu/management VLAN'ından gelen HTTP isteklerine yanıt vermelidir. Misafir VLAN'ından bu cihaza doğrudan erişim engellenmelidir. | Firewall/VLAN katmanında yapılır (aşağıya bkz.) |
| **API Key Koruması** | İstekler statik/yapılandırılabilir bir `X-API-KEY` başlığı ile doğrulanmalıdır. | `EncryptedSharedPreferences` + sabit-zamanlı karşılaştırma |
| **Rate Limiting** | Aynı numaraya 1 dakika içinde birden fazla OTP isteği gelmesi engellenmelidir. | `RateLimiter` (numara başına 60 sn cooldown) |
| **KVKK / Log Güvenliği** | Loglarda hassas veriler maskelenmeli, loglar periyodik olarak temizlenmelidir. | `LogMasker` + retention temizliği (varsayılan 30 gün) |

---

## 5. İzinler ve Sistem Konfigürasyonu

### Gerekli İzinler (Android Manifest)

* `android.permission.SEND_SMS`
* `android.permission.INTERNET`
* `android.permission.ACCESS_NETWORK_STATE`
* `android.permission.FOREGROUND_SERVICE` + `FOREGROUND_SERVICE_SPECIAL_USE`
* `android.permission.WAKE_LOCK`
* `android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`
* `android.permission.POST_NOTIFICATIONS` (Android 13+)
* `android.permission.RECEIVE_BOOT_COMPLETED` (yeniden başlatmada otomatik ayağa kalkma)

### Cihaz Donanım & OS Ayarları

1. **Sabit IP:** Cihaza yerel ağ DHCP/Firewall üzerinden Statik IP (Rezervasyon) tanımlanmalıdır.
2. **Pil Optimizasyonu:** Uygulama içindeki *"Pil Optimizasyonunu Kapat"* butonu ile *Unrestricted* seçilmelidir.
3. **Wi-Fi Uyku İlkesi:** *Ekran kapalıyken Wi-Fi açık kalsın* kuralı aktif edilmelidir.
4. **SIM Pin Kilidi:** Cihaz yeniden başladığında SIM kilitli kalmaması için SIM PIN kilidi kaldırılmalıdır.

---

## 6. Proje Yapısı

```
gateway/
├── settings.gradle.kts
├── build.gradle.kts                 # Kök build script
├── gradle/libs.versions.toml        # Sürüm kataloğu
├── app/
│   ├── build.gradle.kts
│   ├── proguard-rules.pro
│   └── src/
│       ├── main/
│       │   ├── AndroidManifest.xml
│       │   ├── java/com/slnmoda/smsgateway/
│       │   │   ├── SmsGatewayApp.kt
│       │   │   ├── MainActivity.kt          # Yönetim ekranı (izinler, IP, API key)
│       │   │   ├── config/AppConfig.kt      # Şifreli yapılandırma + API anahtarı
│       │   │   ├── util/PhoneValidator.kt   # E.164 doğrulama
│       │   │   ├── util/LogMasker.kt        # KVKK maskeleme
│       │   │   ├── data/                    # Room: Entity / DAO / DB / Repository
│       │   │   ├── server/
│       │   │   │   ├── GatewayHttpServer.kt # NanoHTTPD — API uçları
│       │   │   │   ├── RateLimiter.kt
│       │   │   │   └── dto/Dtos.kt
│       │   │   ├── sms/SmsSender.kt         # SmsManager sarmalayıcı
│       │   │   └── service/
│       │   │       ├── GatewayService.kt    # Foreground Service + WakeLock
│       │   │       └── BootReceiver.kt
│       │   └── res/                         # layout, strings, tema, ikonlar
│       └── test/java/...                    # PhoneValidator & RateLimiter birim testleri
```

---

## 7. Kurulum ve Çalıştırma

### Gereksinimler
- **Android Studio Ladybug (2024.2)** veya üstü — bünyesindeki JDK 17 kullanılır.
- Fiziksel bir Android cihaz (min. **Android 8.0 / API 26**), takılı ve aktif SIM kart.
- SMS emülatörde gönderilemez; **gerçek cihaz gerekir.**

### Adımlar
1. Projeyi Android Studio'da açın. Gradle wrapper ve bağımlılıklar otomatik indirilir.
   > CLI'den derleyecekseniz önce `gradle wrapper --gradle-version 8.11.1` ile wrapper'ı üretin (bu repoda binary `gradle-wrapper.jar` yer almaz).
2. Cihazı USB ile bağlayıp **Run 'app'** ile kurun.
3. Açılan ekranda **Servisi Başlat** → SMS ve bildirim izinlerini verin.
4. **Pil Optimizasyonunu Kapat** butonuyla uygulamayı *Unrestricted* yapın.
5. Ekranda görünen `http://<cihaz-ip>:8080/...` adresini ve **X-API-KEY**'i not alın.
   > API anahtarı ilk açılışta cihaz üzerinde kriptografik olarak üretilir ve şifreli saklanır. **Anahtarı Yenile** ile değiştirilebilir.

### Örnek İstek (curl)

```bash
curl -X POST http://192.168.1.50:8080/api/v1/sms/send \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: <UYGULAMADAKI_ANAHTAR>" \
  -d '{"phone":"+905321112233","message":"Dogrulama kodunuz: 481920"}'
```

### Testler

```bash
./gradlew testDebugUnitTest
```

---

## 8. Üretim / Operasyon Notları

- **Ağ izolasyonu asıl olarak firewall/VLAN katmanında sağlanır.** Cihaz management VLAN'ında olmalı; misafir VLAN'ından `8080` portuna erişim firewall kuralıyla kapatılmalıdır. Uygulama tüm arayüzlerde (`0.0.0.0`) dinler; sınırlamayı ağ katmanı yapar.
- **Foreground Service tipi `specialUse`:** Kalıcı yerel HTTP+SMS köprüsü standart FGS tiplerinden hiçbirine girmez. Uygulama **Google Play'e yüklenecekse** `specialUse` gerekçesi (Manifest'teki `PROPERTY_SPECIAL_USE_FGS_SUBTYPE`) Play Console'da onaylatılmalıdır. Şirket içi (sideload/MDM) dağıtımda bu gerekmez.
- **Tek yönlü servis:** Yalnızca SMS *gönderir*; gelen SMS okumaz (`RECEIVE_SMS`/`READ_SMS` istemez) — saldırı yüzeyi minimumdur.
- **HTTP (TLS yok):** Trafik yerel güvenli ağda kaldığı varsayılır. Uçtan uca şifreleme gerekirse cihaz önüne reverse-proxy (nginx + TLS) konumlandırılabilir.
- **Log saklama:** Varsayılan 30 gün; `AppConfig.logRetentionDays` ile ayarlanır. Servis her başladığında süresi dolan kayıtlar silinir.
```
