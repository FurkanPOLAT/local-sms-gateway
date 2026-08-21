using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using CaptivePortalSms.Options;
using CaptivePortalSms.Otp;
using Microsoft.Extensions.Options;

namespace CaptivePortalSms.Radius;

/// <summary>
/// Minimal RADIUS (RFC 2865) kimlik doğrulama sunucusu. Yalnızca Access-Request /
/// PAP destekler. FortiGate'ten gelen telefon+OTP'yi <see cref="IOtpService"/> ile
/// doğrular; başarılıysa Access-Accept, değilse Access-Reject döner.
/// SharedSecret tanımlı değilse dinleyici başlamaz.
/// </summary>
public sealed class RadiusServer : BackgroundService
{
    private const byte AccessRequest = 1;
    private const byte AccessAccept = 2;
    private const byte AccessReject = 3;

    private readonly IServiceScopeFactory _scopes;
    private readonly RadiusOptions _opt;
    private readonly ILogger<RadiusServer> _log;

    public RadiusServer(IServiceScopeFactory scopes, IOptions<RadiusOptions> opt, ILogger<RadiusServer> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_opt.SharedSecret))
        {
            _log.LogWarning("RADIUS kapali (SharedSecret tanimli degil).");
            return;
        }

        UdpClient udp;
        try { udp = new UdpClient(_opt.Port); }
        catch (Exception ex) { _log.LogError(ex, "RADIUS {Port} portu acilamadi.", _opt.Port); return; }

        _log.LogInformation("RADIUS dinliyor: UDP {Port}", _opt.Port);
        using (udp)
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await udp.ReceiveAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _log.LogError(ex, "RADIUS alim hatasi"); continue; }

                _ = HandleAsync(udp, res, ct);
            }
        }
    }

    private async Task HandleAsync(UdpClient udp, UdpReceiveResult res, CancellationToken ct)
    {
        try
        {
            var reply = await BuildReplyAsync(res.Buffer, res.RemoteEndPoint, ct);
            if (reply is not null) await udp.SendAsync(reply, reply.Length, res.RemoteEndPoint);
        }
        catch (Exception ex) { _log.LogError(ex, "RADIUS islem hatasi"); }
    }

    private async Task<byte[]?> BuildReplyAsync(byte[] pkt, IPEndPoint from, CancellationToken ct)
    {
        if (pkt.Length < 20 || pkt[0] != AccessRequest) return null;

        var id = pkt[1];
        var declaredLen = (pkt[2] << 8) | pkt[3];
        var len = Math.Min(declaredLen, pkt.Length);
        var reqAuth = new byte[16];
        Buffer.BlockCopy(pkt, 4, reqAuth, 0, 16);

        string? userName = null, mac = null, framedIp = null;
        byte[]? encPass = null;
        var hasMsgAuth = false;

        var i = 20;
        while (i + 2 <= len)
        {
            int type = pkt[i], attrLen = pkt[i + 1];
            if (attrLen < 2 || i + attrLen > len) break;
            int vpos = i + 2, vlen = attrLen - 2;
            switch (type)
            {
                case 1: userName = Encoding.UTF8.GetString(pkt, vpos, vlen); break;   // User-Name
                case 2:                                                               // User-Password (PAP)
                    encPass = new byte[vlen];
                    Buffer.BlockCopy(pkt, vpos, encPass, 0, vlen);
                    break;
                case 8: if (vlen == 4) framedIp = $"{pkt[vpos]}.{pkt[vpos + 1]}.{pkt[vpos + 2]}.{pkt[vpos + 3]}"; break;
                case 31: mac = Encoding.UTF8.GetString(pkt, vpos, vlen); break;       // Calling-Station-Id (MAC)
                case 80: hasMsgAuth = true; break;                                    // Message-Authenticator
            }
            i += attrLen;
        }

        var secret = Encoding.UTF8.GetBytes(_opt.SharedSecret);
        var accept = false;

        if (!string.IsNullOrWhiteSpace(userName) && encPass is { Length: > 0 })
        {
            var otpCode = DecryptPap(encPass, secret, reqAuth);
            using var scope = _scopes.CreateScope();
            var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();
            var ip = framedIp ?? from.Address.ToString();
            var result = await otp.VerifyAsync(userName.Trim(), otpCode, ip, mac, ct);
            accept = result.Status == OtpVerifyStatus.Verified;
            _log.LogInformation("RADIUS {User} -> {Res}", Mask(userName), accept ? "ACCEPT" : "REJECT");
        }

        return BuildResponse(accept ? AccessAccept : AccessReject, id, reqAuth, secret, hasMsgAuth);
    }

    /// <summary>RFC 2865 §5.2 PAP çözme.</summary>
    private static string DecryptPap(byte[] enc, byte[] secret, byte[] reqAuth)
    {
        var blocks = enc.Length - (enc.Length % 16);
        if (blocks == 0) return "";
        var outb = new byte[blocks];
        var prev = reqAuth;
        for (var off = 0; off < blocks; off += 16)
        {
            var seed = new byte[secret.Length + 16];
            Buffer.BlockCopy(secret, 0, seed, 0, secret.Length);
            Buffer.BlockCopy(prev, 0, seed, secret.Length, 16);
            var b = MD5.HashData(seed);
            for (var j = 0; j < 16; j++) outb[off + j] = (byte)(enc[off + j] ^ b[j]);
            prev = enc.AsSpan(off, 16).ToArray();
        }
        var end = outb.Length;
        while (end > 0 && outb[end - 1] == 0) end--;
        return Encoding.UTF8.GetString(outb, 0, end);
    }

    /// <summary>Access-Accept/Reject yanıtı; Response Authenticator (+ istenirse Message-Authenticator).</summary>
    private static byte[] BuildResponse(byte code, byte id, byte[] reqAuth, byte[] secret, bool includeMsgAuth)
    {
        var attrLen = includeMsgAuth ? 18 : 0;
        var length = 20 + attrLen;
        var pkt = new byte[length];
        pkt[0] = code;
        pkt[1] = id;
        pkt[2] = (byte)(length >> 8);
        pkt[3] = (byte)(length & 0xFF);
        Buffer.BlockCopy(reqAuth, 0, pkt, 4, 16); // önce Request Authenticator

        if (includeMsgAuth)
        {
            pkt[20] = 80;  // Message-Authenticator
            pkt[21] = 18;  // 2 + 16
            // değer şu an 16 sıfır; HMAC-MD5 bunun üzerinden hesaplanır
            using var hmac = new HMACMD5(secret);
            var mac = hmac.ComputeHash(pkt);
            Buffer.BlockCopy(mac, 0, pkt, 22, 16);
        }

        // Response Authenticator = MD5(pkt + secret), Authenticator alanı Request Auth iken
        var buf = new byte[pkt.Length + secret.Length];
        Buffer.BlockCopy(pkt, 0, buf, 0, pkt.Length);
        Buffer.BlockCopy(secret, 0, buf, pkt.Length, secret.Length);
        var respAuth = MD5.HashData(buf);
        Buffer.BlockCopy(respAuth, 0, pkt, 4, 16);
        return pkt;
    }

    private static string Mask(string phone) =>
        phone.Length <= 6 ? "****" : $"{phone[..Math.Min(5, phone.Length)]}****";
}
