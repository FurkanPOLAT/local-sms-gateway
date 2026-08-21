using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CaptivePortalSms.Compliance;

/// <summary>
/// Aydınlatma metnini diskteki dosyadan yükler; sürümünü ve içeriğinin SHA-256
/// özetini hesaplar. Rıza kaydı bu sürüm ve hash'e referans verir; böylece
/// "kullanıcı hangi metni onayladı" sonradan kanıtlanabilir.
/// </summary>
public sealed partial class ConsentPolicyProvider
{
    public string Version { get; }
    public string Text { get; }
    public string Hash { get; }

    public ConsentPolicyProvider(IHostEnvironment env, ILogger<ConsentPolicyProvider> logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Legal", "kvkk-aydinlatma-metni.v1.md");
        var raw = File.Exists(path) ? File.ReadAllText(path) : "";
        if (raw.Length == 0)
            logger.LogWarning("Aydınlatma metni bulunamadı: {Path}", path);

        var m = VersionRegex().Match(raw);
        Version = m.Success ? m.Groups[1].Value : "1.0";

        // HTML yorumlarını (<!-- ... -->) kullanıcıya göstermeden çıkar.
        Text = CommentRegex().Replace(raw, "").Trim();
        Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));
    }

    [GeneratedRegex(@"SÜRÜM:\s*([0-9][0-9.]*)")]
    private static partial Regex VersionRegex();

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();
}
