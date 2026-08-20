# =====================================================================
#  Local SMS Gateway - Test Arayuzu (PowerShell / WinForms)
#  Calistirma:  powershell.exe -ExecutionPolicy Bypass -File .\gateway-ui.ps1
#  (Tarayici olmadigi icin CORS sorunu yoktur; APK yeniden derlenmez.)
# =====================================================================

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
try { Add-Type -AssemblyName System.Net.Http } catch { }  # PS 5.1 icin

# ---- Ayar dosyasi (IP/port/numara hatirlanir; API anahtari opsiyonel) ----
$settingsPath = Join-Path $env:APPDATA "SlnSmsGateway\ui-settings.json"
function Load-Settings {
    if (Test-Path $settingsPath) {
        try { return Get-Content $settingsPath -Raw | ConvertFrom-Json } catch { }
    }
    return [pscustomobject]@{ Ip=""; Port="8080"; ApiKey=""; Phone=""; Prefix=$true; SaveKey=$false }
}
function Save-Settings($s) {
    $dir = Split-Path $settingsPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $s | ConvertTo-Json | Set-Content $settingsPath -Encoding UTF8
}
$cfg = Load-Settings

# ---- HTTP istemcisi (4xx/5xx'te exception atmaz; yaniti okuyabiliriz) ----
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(12)

function Invoke-Gateway($method, $url, $apiKey, $jsonBody) {
    try {
        $req = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::new($method), $url)
        if ($apiKey) { [void]$req.Headers.TryAddWithoutValidation("X-API-KEY", $apiKey) }
        if ($jsonBody) {
            $req.Content = [System.Net.Http.StringContent]::new(
                $jsonBody, [System.Text.Encoding]::UTF8, "application/json")
        }
        $resp = $http.SendAsync($req).GetAwaiter().GetResult()
        $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{ Ok=$true; Code=[int]$resp.StatusCode; Body=$body }
    } catch {
        $msg = $_.Exception.Message
        if ($_.Exception.InnerException) { $msg = $_.Exception.InnerException.Message }
        return [pscustomobject]@{ Ok=$false; Code=0; Body="BAGLANTI HATASI: $msg" }
    }
}

# ---- Form ----
$form = New-Object System.Windows.Forms.Form
$form.Text = "SLNMODA SMS Gateway - Test Arayuzu"
$form.Size = New-Object System.Drawing.Size(520, 620)
$form.StartPosition = "CenterScreen"
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9)

function Add-Label($text, $y) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text; $l.Location = New-Object System.Drawing.Point(15, $y)
    $l.Size = New-Object System.Drawing.Size(120, 22)
    $form.Controls.Add($l); return $l
}
function Add-Text($x, $y, $w, $val) {
    $t = New-Object System.Windows.Forms.TextBox
    $t.Location = New-Object System.Drawing.Point($x, $y)
    $t.Size = New-Object System.Drawing.Size($w, 24); $t.Text = "$val"
    $form.Controls.Add($t); return $t
}

Add-Label "Gateway IP:" 20
$txtIp = Add-Text 140 18 200 $cfg.Ip
$lblPort = Add-Label "Port:" 20
$lblPort.Location = New-Object System.Drawing.Point(350, 20)
$lblPort.Size = New-Object System.Drawing.Size(45, 22)
$txtPort = New-Object System.Windows.Forms.TextBox
$txtPort.Location = New-Object System.Drawing.Point(400, 18)
$txtPort.Size = New-Object System.Drawing.Size(80, 24); $txtPort.Text = "$($cfg.Port)"
$form.Controls.Add($txtPort)

Add-Label "X-API-KEY:" 54
$txtKey = Add-Text 140 52 340 $cfg.ApiKey
$txtKey.UseSystemPasswordChar = $true

$chkShowKey = New-Object System.Windows.Forms.CheckBox
$chkShowKey.Text = "Anahtari goster"
$chkShowKey.Location = New-Object System.Drawing.Point(140, 82)
$chkShowKey.Size = New-Object System.Drawing.Size(130, 22)
$chkShowKey.Add_CheckedChanged({ $txtKey.UseSystemPasswordChar = -not $chkShowKey.Checked })
$form.Controls.Add($chkShowKey)

$chkSaveKey = New-Object System.Windows.Forms.CheckBox
$chkSaveKey.Text = "Anahtari kaydet"
$chkSaveKey.Location = New-Object System.Drawing.Point(290, 82)
$chkSaveKey.Size = New-Object System.Drawing.Size(190, 22)
$chkSaveKey.Checked = [bool]$cfg.SaveKey
$form.Controls.Add($chkSaveKey)

Add-Label "Telefon (E.164):" 116
$txtPhone = Add-Text 140 114 200 $cfg.Phone

Add-Label "Mesaj:" 150
$txtMsg = New-Object System.Windows.Forms.TextBox
$txtMsg.Location = New-Object System.Drawing.Point(140, 148)
$txtMsg.Size = New-Object System.Drawing.Size(340, 70)
$txtMsg.Multiline = $true; $txtMsg.ScrollBars = "Vertical"
$txtMsg.Text = "Dogrulama kodunuz: 481920"
$form.Controls.Add($txtMsg)

$chkPrefix = New-Object System.Windows.Forms.CheckBox
$chkPrefix.Text = "Mesaj basina 'SLNMODA:' ekle  (client tarafi marka)"
$chkPrefix.Location = New-Object System.Drawing.Point(140, 224)
$chkPrefix.Size = New-Object System.Drawing.Size(340, 22)
$chkPrefix.Checked = [bool]$cfg.Prefix
$form.Controls.Add($chkPrefix)

# ---- Butonlar ----
$btnHealth = New-Object System.Windows.Forms.Button
$btnHealth.Text = "Saglik Kontrolu"
$btnHealth.Location = New-Object System.Drawing.Point(140, 256)
$btnHealth.Size = New-Object System.Drawing.Size(160, 34)
$form.Controls.Add($btnHealth)

$btnSend = New-Object System.Windows.Forms.Button
$btnSend.Text = "SMS Gonder"
$btnSend.Location = New-Object System.Drawing.Point(320, 256)
$btnSend.Size = New-Object System.Drawing.Size(160, 34)
$btnSend.BackColor = [System.Drawing.Color]::FromArgb(21, 101, 192)
$btnSend.ForeColor = [System.Drawing.Color]::White
$form.Controls.Add($btnSend)

# ---- Cikti ----
$out = New-Object System.Windows.Forms.RichTextBox
$out.Location = New-Object System.Drawing.Point(15, 305)
$out.Size = New-Object System.Drawing.Size(465, 255)
$out.ReadOnly = $true; $out.BackColor = "White"
$out.Font = New-Object System.Drawing.Font("Consolas", 9)
$form.Controls.Add($out)

function Log-Line($text, $color) {
    $out.SelectionStart = $out.TextLength
    $out.SelectionColor = $color
    $out.AppendText("$text`n")
    $out.ScrollToCaret()
}

function Persist {
    $keyToSave = ""
    if ($chkSaveKey.Checked) { $keyToSave = $txtKey.Text }
    Save-Settings ([pscustomobject]@{
        Ip=$txtIp.Text; Port=$txtPort.Text
        ApiKey=$keyToSave
        Phone=$txtPhone.Text; Prefix=$chkPrefix.Checked; SaveKey=$chkSaveKey.Checked
    })
}

function Base-Url { "http://$($txtIp.Text.Trim()):$($txtPort.Text.Trim())/api/v1" }

$btnHealth.Add_Click({
    Persist
    Log-Line "`n>> GET /health ..." ([System.Drawing.Color]::Gray)
    $r = Invoke-Gateway "GET" "$(Base-Url)/health" $null $null
    $c = if ($r.Code -eq 200) { [System.Drawing.Color]::Green } else { [System.Drawing.Color]::Red }
    Log-Line "HTTP $($r.Code)  $($r.Body)" $c
})

$btnSend.Add_Click({
    Persist
    $phone = $txtPhone.Text.Trim()
    $msg = $txtMsg.Text
    if ($chkPrefix.Checked) { $msg = "SLNMODA: $msg" }   # <-- marka client'ta ekleniyor
    if (-not $phone -or -not $msg) {
        Log-Line "Telefon ve mesaj bos olamaz." ([System.Drawing.Color]::Red); return
    }
    $json = @{ phone=$phone; message=$msg } | ConvertTo-Json -Compress
    Log-Line "`n>> POST /sms/send  ($phone)" ([System.Drawing.Color]::Gray)
    Log-Line "   gonderilen metin: $msg" ([System.Drawing.Color]::DimGray)
    $r = Invoke-Gateway "POST" "$(Base-Url)/sms/send" $txtKey.Text $json
    $c = switch ($r.Code) {
        200 { [System.Drawing.Color]::Green }
        default { [System.Drawing.Color]::Red }
    }
    Log-Line "HTTP $($r.Code)  $($r.Body)" $c
})

Log-Line "Hazir. IP + X-API-KEY girip 'Saglik Kontrolu' ile baslayin." ([System.Drawing.Color]::Black)
[void]$form.ShowDialog()
