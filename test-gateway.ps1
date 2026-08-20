# =====================================================================
#  Local SMS Gateway - Terminal Test Betigi
#  Kullanim:  .\test-gateway.ps1 -Ip 192.168.1.50 -ApiKey "ANAHTAR" -Phone "+905321112233"
# =====================================================================
param(
    [Parameter(Mandatory=$true)] [string]$Ip,
    [Parameter(Mandatory=$true)] [string]$ApiKey,
    [Parameter(Mandatory=$true)] [string]$Phone,
    [int]$Port = 8080
)

$base = "http://${Ip}:${Port}/api/v1"
Write-Host "`n== Gateway test: $base ==`n" -ForegroundColor Cyan

function Show($title, $color) { Write-Host "`n--- $title ---" -ForegroundColor $color }

# Ham HTTP durum kodunu almak icin yardimci (hata kodlarinda da yanit okunur)
function Invoke-Api($method, $path, $headers, $body) {
    try {
        $r = Invoke-WebRequest -Uri "$base$path" -Method $method -Headers $headers `
             -ContentType "application/json" -Body $body -ErrorAction Stop
        return [pscustomobject]@{ Code = [int]$r.StatusCode; Body = $r.Content }
    } catch {
        $resp = $_.Exception.Response
        if ($resp) {
            $code = [int]$resp.StatusCode
            $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
            return [pscustomobject]@{ Code = $code; Body = $reader.ReadToEnd() }
        }
        return [pscustomobject]@{ Code = -1; Body = $_.Exception.Message }
    }
}

$auth = @{ "X-API-KEY" = $ApiKey }

# 1) Saglik kontrolu
Show "1) Health (200 bekleniyor)" "Yellow"
$r = Invoke-Api "GET" "/health" @{} $null
Write-Host "HTTP $($r.Code)  ->  $($r.Body)"

# 2) Basarili SMS (200 + telefona SMS dusmeli)
Show "2) Gecerli SMS (200 bekleniyor)" "Green"
$body = @{ phone = $Phone; message = "Test kodu: 481920" } | ConvertTo-Json -Compress
$r = Invoke-Api "POST" "/sms/send" $auth $body
Write-Host "HTTP $($r.Code)  ->  $($r.Body)"

# 3) Yanlis API key (401 bekleniyor)
Show "3) Yanlis API key (401 bekleniyor)" "Red"
$r = Invoke-Api "POST" "/sms/send" @{ "X-API-KEY" = "yanlis-anahtar" } $body
Write-Host "HTTP $($r.Code)  ->  $($r.Body)"

# 4) Rate limit - ayni numaraya hemen ikinci istek (429 bekleniyor)
Show "4) Rate limit (429 bekleniyor)" "Magenta"
$r = Invoke-Api "POST" "/sms/send" $auth $body
Write-Host "HTTP $($r.Code)  ->  $($r.Body)"

# 5) Hatali telefon formati (400 bekleniyor)
Show "5) Hatali telefon (400 bekleniyor)" "DarkYelloW"
$bad = @{ phone = "05321112233"; message = "test" } | ConvertTo-Json -Compress
$r = Invoke-Api "POST" "/sms/send" $auth $bad
Write-Host "HTTP $($r.Code)  ->  $($r.Body)"

Write-Host "`n== Test tamamlandi ==`n" -ForegroundColor Cyan
