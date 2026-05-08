using System.Security.Cryptography;
using System.Text;
using GraNAS.Sharing.Services.Interfaces;

namespace GraNAS.Sharing.Services.Implementations;

public sealed class TokenEncryptionService : ITokenEncryptionService
{
    private readonly byte[] _key;

    public TokenEncryptionService(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("Key must be exactly 32 bytes (AES-256).", nameof(key));
        _key = key;
    }

    public string Encrypt(string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(iv, pt, ct, tag);

        // Format: base64(iv[12] || tag[16] || ciphertext)
        var blob = new byte[12 + tag.Length + ct.Length];
        Buffer.BlockCopy(iv, 0, blob, 0, 12);
        Buffer.BlockCopy(tag, 0, blob, 12, tag.Length);
        Buffer.BlockCopy(ct, 0, blob, 12 + tag.Length, ct.Length);
        return Convert.ToBase64String(blob);
    }

    public string Decrypt(string ciphertext)
    {
        var blob = Convert.FromBase64String(ciphertext);
        var iv = blob[..12];
        var tag = blob[12..28];
        var ct = blob[28..];
        var pt = new byte[ct.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(iv, ct, tag, pt);
        return Encoding.UTF8.GetString(pt);
    }
}
