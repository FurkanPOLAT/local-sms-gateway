# Ubuntu Kurulum Rehberi — SLN Captive Portal (5651/KVKK)

Bu rehber, `.NET 8` Captive Portal servisini bir **Ubuntu sunucuya** (Docker'sız,
`systemd` ile) kurmak içindir. Adım adım ilerle; her bölümün sonundaki **doğrulama**
komutuyla o adımın çalıştığını gör.

> Mimari hatırlatma:
> ```
> [Misafir cihaz] → [FortiGate: yakala/yönlendir] → [Ubuntu: Captive Portal (.NET)] → [Android Gateway: SMS]
>                                                         └─ compliance.db (KVKK rıza + 5651 log)
> ```

---

## 0. Ön Koşullar

- Ubuntu Server 22.04 veya 24.04 LTS.
- Sunucuya **statik IP** (router/FortiGate DHCP rezervasyonu). Örn. `192.168.10.20`.
- Yönetici (sudo) yetkili bir kullanıcı.
- Android Gateway telefonu aynı ağda, statik IP'li ve servisi açık.
- Sunucunun **saati doğru** olmalı (yasal zaman damgaları için kritik — Bölüm 1).

---

## 1. Sistem Hazırlığı

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y git curl ca-certificates
```

**Saat senkronizasyonu (5651 için kritik):**
```bash
sudo timedatectl set-timezone Europe/Istanbul
sudo timedatectl set-ntp true
timedatectl status        # "System clock synchronized: yes"
```

---

## 2. .NET 8 SDK Kurulumu

Sunucuda hem kodu çekip hem yayınlayacağımız için **SDK** kuruyoruz (SDK, runtime'ı da içerir):
```bash
sudo apt install -y dotnet-sdk-8.0
```
> Paket bulunamazsa Microsoft deposunu ekle (bir kez):
> ```bash
> wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O /tmp/ms.deb
> sudo dpkg -i /tmp/ms.deb && sudo apt update && sudo apt install -y dotnet-sdk-8.0
> ```
> Alternatif: SDK'yı sunucuya kurmak istemezsen, kendi bilgisayarında
> `dotnet publish -c Release -o publish` yapıp `publish` klasörünü `scp` ile
> `/opt/sln/portal`'a kopyala; sunucuda sadece `aspnetcore-runtime-8.0` yeter.

**Doğrulama:** `dotnet --info` → ".NET SDK 8.x" görünmeli.

---

## 3. Kodu Alma ve Yayınlama

```bash
sudo mkdir -p /opt/sln && sudo chown $USER:$USER /opt/sln
cd /opt/sln
git clone https://github.com/FurkanPOLAT/local-sms-gateway.git
cd local-sms-gateway/clients/dotnet/CaptivePortalSms
```

**Yayınla** (SDK yoksa, kendi bilgisayarında `dotnet publish` yapıp `scp` ile
`/opt/sln/portal`'a kopyalayabilirsin):
```bash
dotnet publish -c Release -o /opt/sln/portal
```

**Doğrulama:** `ls /opt/sln/portal` içinde `CaptivePortalSms.dll`, `wwwroot/`, `Legal/` olmalı.

---

## 4. Servis Kullanıcısı, Veri Klasörü ve Gizli Değerler

```bash
sudo useradd --system --no-create-home slnportal
sudo mkdir -p /opt/sln/portal/data
sudo chown -R slnportal:slnportal /opt/sln/portal
```

**Ortam dosyası (gizli değerler — imaja/koda/git'e girmez):**
```bash
sudo nano /etc/sln-portal.env
```
İçerik:
```
SmsGateway__BaseUrl=http://<TELEFON-IP>:8080
SmsGateway__ApiKey=<X-API-KEY>
Admin__ApiKey=<güçlü admin anahtarı>
ConnectionStrings__ComplianceDb=Data Source=/opt/sln/portal/data/compliance.db
ASPNETCORE_URLS=http://127.0.0.1:8080
ASPNETCORE_ENVIRONMENT=Production
```
```bash
sudo chmod 600 /etc/sln-portal.env
sudo chown slnportal:slnportal /etc/sln-portal.env
```
> Güçlü admin anahtarı: `openssl rand -hex 24`
> `ASPNETCORE_URLS=127.0.0.1` → dışarıya doğrudan açılmaz; nginx (Bölüm 6) TLS ile sunar.

---

## 5. systemd Servisi

```bash
sudo nano /etc/systemd/system/sln-portal.service
```
```ini
[Unit]
Description=SLN Captive Portal (5651/KVKK)
After=network.target

[Service]
Type=simple
User=slnportal
WorkingDirectory=/opt/sln/portal
EnvironmentFile=/etc/sln-portal.env
ExecStart=/usr/bin/dotnet /opt/sln/portal/CaptivePortalSms.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now sln-portal
sudo systemctl status sln-portal        # active (running)
curl -fsS http://127.0.0.1:8080/health  # {"status":"up"}
```

**Doğrulama:** `/health` → `{"status":"up"}`. Loglar: `journalctl -u sln-portal -f`.

---

## 6. TLS (HTTPS) — nginx reverse proxy

Uygulama içeride HTTP (127.0.0.1:8080) dinler; dışarıya **nginx** HTTPS sunar.

```bash
sudo apt install -y nginx
sudo mkdir -p /etc/nginx/certs
sudo openssl req -x509 -nodes -days 825 -newkey rsa:2048 \
  -keyout /etc/nginx/certs/portal.key -out /etc/nginx/certs/portal.crt \
  -subj "/C=TR/O=SLN Tekstil/CN=portal.sln.local"
```

```bash
sudo nano /etc/nginx/sites-available/sln-portal
```
```nginx
server {
    listen 80;
    server_name portal.sln.local;
    return 301 https://$host$request_uri;
}
server {
    listen 443 ssl;
    server_name portal.sln.local;

    ssl_certificate     /etc/nginx/certs/portal.crt;
    ssl_certificate_key /etc/nginx/certs/portal.key;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```
```bash
sudo ln -s /etc/nginx/sites-available/sln-portal /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

> **Önemli (IP loglama):** nginx arkasında gerçek misafir IP'si `X-Forwarded-For`
> başlığında gelir. 5651 logunda doğru IP için uygulamaya "forwarded headers"
> ayarı gerekir — bunu deploy sırasında birlikte ekleriz. (nginx kullanmaz,
> FortiGate'i doğrudan 8080'e yönlendirirsen gerekmez.)

---

## 7. FortiGate 200F Entegrasyonu (kavramsal — cihaz başında birlikte)

Hedef akış: **misafir bağlanır → portal açılır → OTP başarılı → internet açılır.**

1. **Walled garden:** Auth öncesi misafir yalnızca portal sunucusuna + DNS'e ulaşsın;
   diğer her istek portala yönlendirilsin.
2. **External captive portal:** SSID/VLAN güvenlik ayarında portal tipi = *External*,
   `external-web = https://<UBUNTU-IP-veya-portal.sln.local>`.
3. **OTP sonrası yetkilendirme:** İki yol —
   - **RADIUS (önerilen/güvenli):** FortiGate, phone+OTP'yi RADIUS ile bize sorar;
     OTP doğruysa erişim açılır. OTP atlanamaz.
   - **Paylaşımlı hesap + magic (basit):** Portal, OTP sonrası sabit bir misafir
     hesabıyla FortiGate'e giriş yapar. Daha kolay ama OTP atlama riski taşır.

> Tam yönlendirme parametreleri ve yetkilendirme adımı FortiOS sürümüne + bağlantı
> tipine (FortiAP / kablolu VLAN) göre değişir; cihaz başında birlikte bağlanır.
> Portal, FortiGate'in eklediği **`usermac`**'i zaten yakalayıp 5651 kaydına yazıyor.

**Şimdiden FortiGate'te açılacaklar:**
- Misafir VLAN → portal sunucusunun **443**'üne (auth öncesi exempt) izin.
- Portal sunucusu → Android Gateway (`:8080`) izin.
- Misafir VLAN → portalın **admin uçlarına erişim ENGELLİ** (sadece yönetim VLAN).

---

## 8. Ağ İzolasyonu / Firewall (ISO 27001)

- Portal sunucusu **yönetim/sunucu VLAN'ında**.
- Misafir VLAN → sadece portalın **443**'üne.
- **Admin uçları** (`/api/admin/...`) yalnızca yönetim VLAN'ından (FortiGate kuralı +
  `X-ADMIN-KEY` — iki katman).
- Android Gateway'e (`:8080`) yalnızca portal sunucusu erişebilsin.

Ubuntu yerel güvenlik duvarı (opsiyonel ek katman):
```bash
sudo ufw allow 443/tcp
sudo ufw allow 80/tcp
sudo ufw allow from <YONETIM_VLAN_CIDR> to any port 22
sudo ufw enable
```

---

## 9. Yedekleme (KVKK/ISO)

Yasal kayıtlar `/opt/sln/portal/data/compliance.db` — günlük yedek:
```bash
sudo nano /etc/cron.daily/sln-portal-backup
```
```bash
#!/bin/bash
set -e
DEST=/opt/sln/backups
mkdir -p "$DEST"
STAMP=$(date +%F)
# SQLite tutarlı yedek (.backup) — servis çalışırken güvenli
sqlite3 /opt/sln/portal/data/compliance.db ".backup '$DEST/compliance-$STAMP.db'"
find "$DEST" -name 'compliance-*.db' -mtime +90 -delete
```
```bash
sudo apt install -y sqlite3
sudo chmod +x /etc/cron.daily/sln-portal-backup
```
> Yedekleri **erişimi kısıtlı** (tercihen şifreli) bir yerde tut.

---

## 10. Operasyon

**Güncelleme (yeni sürüm):**
```bash
cd /opt/sln/local-sms-gateway && git pull
cd clients/dotnet/CaptivePortalSms
sudo -u slnportal dotnet publish -c Release -o /opt/sln/portal   # veya kendi PC'nde publish + scp
sudo systemctl restart sln-portal
```

**Anahtar rotasyonu:** Telefonda "Anahtarı Yenile" → `/etc/sln-portal.env` güncelle →
`sudo systemctl restart sln-portal`.

**Zincir bütünlüğü kontrolü (yasal kanıt sağlamlığı):**
```bash
curl -s -H "X-ADMIN-KEY: <ADMIN_KEY>" http://127.0.0.1:8080/api/admin/chain/verify
```

**Yasal talep — kayıt dışa aktarımı:**
```bash
curl -s -H "X-ADMIN-KEY: <ADMIN_KEY>" \
  "http://127.0.0.1:8080/api/admin/access?phone=+905321112233"
```

**Loglar:** `journalctl -u sln-portal -f`

---

## 11. KVKK / ISO 27001 Kontrol Listesi

- [ ] Aydınlatma metnindeki **saklama süresi (2 yıl)** hukuk onaylı.
- [ ] VERBİS kayıt yükümlülüğü uyum birimiyle teyit edildi.
- [ ] `/etc/sln-portal.env` `chmod 600`, sadece `slnportal` erişebiliyor.
- [ ] Admin uçları yalnızca yönetim VLAN'ından erişilebilir.
- [ ] Sunucu saati NTP ile senkron.
- [ ] Günlük yedek çalışıyor, yedekler erişimi kısıtlı yerde.
- [ ] TLS (HTTPS) aktif.
- [ ] Zincir doğrulama (`chain/verify`) düzenli kontrol ediliyor.
- [ ] Anahtar rotasyon planı var.
- [ ] Aydınlatma metni + rıza akışı canlıda çalışıyor.

---

## Sorun giderme

| Belirti | Bakılacak |
|---|---|
| Servis başlamıyor | `sudo systemctl status sln-portal`, `journalctl -u sln-portal -e` |
| `/health` çalışmıyor | `ASPNETCORE_URLS` doğru mu, port çakışması var mı |
| SMS gitmiyor | Telefon IP doğru mu, gateway açık mı, `SmsGateway__ApiKey` doğru mu |
| Admin ucu 401 | `X-ADMIN-KEY` `/etc/sln-portal.env`'deki `Admin__ApiKey` ile aynı mı |
| Loglarda IP yanlış | nginx arkasındaysan "forwarded headers" adımı (Bölüm 6 notu) |
