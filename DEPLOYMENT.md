# Ubuntu Kurulum Rehberi — SLN Captive Portal (5651/KVKK)

Bu rehber, `.NET 8` Captive Portal servisini bir **Ubuntu sunucuya** kurmak içindir.
Adım adım ilerle; her bölümün sonundaki **doğrulama** komutuyla o adımın çalıştığını gör.

> Mimari hatırlatma:
> ```
> [Misafir cihaz] → [FortiGate: yakala/yönlendir] → [Ubuntu: Captive Portal (.NET)] → [Android Gateway: SMS]
>                                                         └─ compliance.db (KVKK rıza + 5651 log)
> ```

İki kurulum yolu var — **birini** seç:
- **A yolu — Docker (önerilen):** tek komutla kurulur, taşınabilir. Bölüm 3A.
- **B yolu — Docker'sız (systemd):** doğrudan .NET runtime. Bölüm 3B.

---

## 0. Ön Koşullar

- Ubuntu Server 22.04 veya 24.04 LTS (temiz kurulum).
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
timedatectl status        # "System clock synchronized: yes" görmelisin
```

**Doğrulama:** `timedatectl status` çıktısında `NTP service: active` ve `synchronized: yes`.

---

## 2. Kodu Sunucuya Alma

```bash
sudo mkdir -p /opt/sln && sudo chown $USER:$USER /opt/sln
cd /opt/sln
git clone https://github.com/FurkanPOLAT/local-sms-gateway.git
cd local-sms-gateway/clients/dotnet/CaptivePortalSms
```

**Doğrulama:** `ls` çıktısında `Dockerfile`, `Program.cs`, `wwwroot/` görünmeli.

---

## 3A. Kurulum — Docker (önerilen)

### 3A.1 Docker kur
```bash
# Docker resmi deposu
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# sudo'suz docker (opsiyonel — yeniden giriş gerekir)
sudo usermod -aG docker $USER
```
**Doğrulama:** `docker --version` ve `docker compose version` sürüm yazmalı.
(Gruba eklediysen çıkış-giriş yap veya `newgrp docker`.)

### 3A.2 Gizli değerleri gir (.env)
```bash
cp .env.example .env
nano .env
```
Şu üç değeri doldur:
```
SMS_GATEWAY_URL=http://<TELEFON-IP>:8080
SMS_GATEWAY_KEY=<telefondaki X-API-KEY>
ADMIN_KEY=<güçlü bir admin anahtarı üret>
```
Sonra izinleri kıs (ISO/KVKK):
```bash
chmod 600 .env
```
> Güçlü admin anahtarı üretmek için: `openssl rand -hex 24`

### 3A.3 Derle ve başlat
```bash
docker compose up -d --build
```
**Doğrulama:**
```bash
docker compose ps                       # durum: running/healthy
curl -fsS http://localhost:8080/health  # {"status":"up"}
```

### 3A.4 Kalıcılık
Yasal kayıtlar `portal-data` adlı Docker volume'ünde (`/app/data/compliance.db`).
Konteyner silinse bile veri durur. Volume yerini görmek için:
```bash
docker volume inspect captiveportalsms_portal-data
```

→ **Bölüm 4'e geç (TLS).**

---

## 3B. Kurulum — Docker'sız (systemd) [alternatif]

### 3B.1 .NET 8 runtime kur
```bash
sudo apt install -y aspnetcore-runtime-8.0
# Eğer paket bulunamazsa Microsoft deposunu ekle:
# https://learn.microsoft.com/dotnet/core/install/linux-ubuntu
dotnet --info
```

### 3B.2 Yayınla
```bash
cd /opt/sln/local-sms-gateway/clients/dotnet/CaptivePortalSms
dotnet publish -c Release -o /opt/sln/portal
```

### 3B.3 Servis kullanıcısı + veri klasörü
```bash
sudo useradd --system --no-create-home slnportal
sudo mkdir -p /opt/sln/portal/data
sudo chown -R slnportal:slnportal /opt/sln/portal
```

### 3B.4 Ortam dosyası (gizli değerler)
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

### 3B.5 systemd servisi
```bash
sudo nano /etc/systemd/system/sln-portal.service
```
İçerik:
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
curl -fsS http://localhost:8080/health  # {"status":"up"}
```

---

## 4. TLS (HTTPS) — nginx reverse proxy

Uygulama içeride HTTP (8080) dinler; dışarıya **nginx** HTTPS sunar.

```bash
sudo apt install -y nginx
```

**Sertifika:** İç ağ için kurumsal iç CA veya self-signed. Hızlı self-signed:
```bash
sudo mkdir -p /etc/nginx/certs
sudo openssl req -x509 -nodes -days 825 -newkey rsa:2048 \
  -keyout /etc/nginx/certs/portal.key -out /etc/nginx/certs/portal.crt \
  -subj "/C=TR/O=SLN Tekstil/CN=portal.sln.local"
```

**nginx site:**
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
> başlığında gelir. 5651 logunda gerçek IP'yi yazmak için uygulamaya "forwarded
> headers" ayarı gerekir — bu bir sonraki kod adımımız (deploy sırasında birlikte
> ekleriz). Docker'da 8080'i doğrudan FortiGate'e verirsen bu gerekmez.

---

## 5. FortiGate Entegrasyonu (kavramsal — cihaz başında birlikte)

Tipik "harici captive portal" akışı:
1. Misafir Wi-Fi'ye bağlanır → FortiGate yakalar.
2. FortiGate, kimliği doğrulanmamış kullanıcıyı **portal URL'imize** yönlendirir
   (FortiGate; `usermac`, `apmac`, `magic` gibi parametreler ekler).
3. Kullanıcı numara + OTP ile doğrular (bizim portal).
4. Portal, FortiGate'e "bu kullanıcıya izin ver" bilgisini geri gönderir
   (FortiOS sürümüne göre `fgtauth` POST'u veya RADIUS).

**Şimdiden yapılacak FortiGate ayarları:**
- Portal sunucusuna (Ubuntu IP) misafir VLAN'ından **80/443** erişimine izin (auth öncesi exempt).
- Portal → Android Gateway (`:8080`) trafiğine izin.
- Misafir VLAN'ından portal sunucusunun **admin uçlarına erişim ENGELLİ** (sadece yönetim VLAN'ı).

> FortiGate tarafındaki tam yönlendirme parametreleri ve geri-bildirim (authorize)
> adımını, cihaz başında FortiOS sürümüne göre birlikte bağlarız. Bu adımda kod
> tarafında MAC'i URL'den alıp erişim loguna yazacağız.

---

## 6. Ağ İzolasyonu / Firewall (ISO 27001)

- Portal sunucusu **yönetim/sunucu VLAN'ında** olmalı.
- Misafir VLAN → sadece portalın **80/443**'üne ulaşsın.
- **Admin uçları** (`/api/admin/...`) yalnızca yönetim VLAN'ından erişilebilir olsun
  (FortiGate kuralı + zaten `X-ADMIN-KEY` koruması var — iki katman).
- Android Gateway'e (`:8080`) yalnızca portal sunucusu erişebilsin.

Ubuntu yerel güvenlik duvarı (opsiyonel ek katman):
```bash
sudo ufw allow 443/tcp
sudo ufw allow 80/tcp
sudo ufw allow from <YONETIM_VLAN_CIDR> to any port 22
sudo ufw enable
```

---

## 7. Yedekleme (KVKK/ISO)

Yasal kayıtlar (`compliance.db`) düzenli yedeklenmeli.

**Docker (volume) yedeği — günlük cron:**
```bash
sudo nano /etc/cron.daily/sln-portal-backup
```
```bash
#!/bin/bash
set -e
DEST=/opt/sln/backups
mkdir -p "$DEST"
STAMP=$(date +%F)
docker run --rm -v captiveportalsms_portal-data:/data -v "$DEST":/backup \
  alpine sh -c "cp /data/compliance.db /backup/compliance-$STAMP.db"
# 90 günden eski yedekleri sil
find "$DEST" -name 'compliance-*.db' -mtime +90 -delete
```
```bash
sudo chmod +x /etc/cron.daily/sln-portal-backup
```
> Docker'sız kurulumda `compliance.db` yolu `/opt/sln/portal/data/compliance.db`;
> aynı mantıkla `cp` ile yedekle.
> Yedekleri **şifreli/erişimi kısıtlı** bir yerde tut.

---

## 8. Operasyon

**Güncelleme (yeni sürüm):**
```bash
cd /opt/sln/local-sms-gateway
git pull
cd clients/dotnet/CaptivePortalSms
docker compose up -d --build      # Docker
# veya: dotnet publish + sudo systemctl restart sln-portal   (systemd)
```

**Anahtar rotasyonu:** Telefonda "Anahtarı Yenile" → `.env` (veya `/etc/sln-portal.env`)
güncelle → servisi yeniden başlat.

**Zincir bütünlüğü kontrolü (yasal kanıt sağlamlığı):**
```bash
curl -s -H "X-ADMIN-KEY: <ADMIN_KEY>" http://localhost:8080/api/admin/chain/verify
# {"valid":true,...} beklenir
```

**Yasal talep — kayıt dışa aktarımı:**
```bash
curl -s -H "X-ADMIN-KEY: <ADMIN_KEY>" \
  "http://localhost:8080/api/admin/access?phone=+905321112233" | jq .
```

**Loglar:**
```bash
docker compose logs -f            # Docker
journalctl -u sln-portal -f       # systemd
```

---

## 9. KVKK / ISO 27001 Kontrol Listesi

- [ ] Aydınlatma metnindeki **saklama süresi (2 yıl)** hukuk onaylı.
- [ ] VERBİS kayıt yükümlülüğü uyum birimiyle teyit edildi.
- [ ] `.env` / env dosyası `chmod 600`, sadece yetkili erişebiliyor.
- [ ] Admin uçları yalnızca yönetim VLAN'ından erişilebilir.
- [ ] Sunucu saati NTP ile senkron.
- [ ] Günlük yedek çalışıyor, yedekler erişimi kısıtlı yerde.
- [ ] TLS aktif (HTTPS).
- [ ] Zincir doğrulama (`chain/verify`) düzenli kontrol ediliyor.
- [ ] Anahtar rotasyon planı var.
- [ ] Aydınlatma metni + rıza akışı canlıda çalışıyor.

---

## Sorun giderme

| Belirti | Bakılacak |
|---|---|
| Portal açılmıyor | `docker compose ps` / `systemctl status`, `curl localhost:8080/health` |
| SMS gitmiyor | Telefon IP doğru mu, gateway servisi açık mı, `.env`'deki `SMS_GATEWAY_KEY` doğru mu |
| Admin ucu 401 | `X-ADMIN-KEY` header'ı `.env`'deki `ADMIN_KEY` ile aynı mı |
| Loglarda IP yanlış | nginx arkasındaysan "forwarded headers" adımı (Bölüm 4 notu) |
