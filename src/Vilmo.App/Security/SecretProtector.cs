using System.Security.Cryptography;
using System.Text;

namespace Vilmo.Security;

public sealed class SecretProtector
{
    readonly byte[] _key;

    public SecretProtector(IConfiguration config)
    {
        var hex = config["DATA_PROTECTION_KEY"] ?? config["Vilmo:DataProtectionKey"] ?? "";
        _key = hex.Length >= 64
            ? Convert.FromHexString(hex[..64])
            : SHA256.HashData(Encoding.UTF8.GetBytes(hex.Length == 0 ? "vilmo-dev-data-protection-key" : hex));
    }

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(_key, 16);
        gcm.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string Unprotect(string packed)
    {
        var all = Convert.FromBase64String(packed);
        var nonce = all[..12];
        var tag = all[12..28];
        var cipher = all[28..];
        var plain = new byte[cipher.Length];
        using var gcm = new AesGcm(_key, 16);
        gcm.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    public string Mask(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= 4) return "••••";
        return "••••" + value[^2..];
    }
}
