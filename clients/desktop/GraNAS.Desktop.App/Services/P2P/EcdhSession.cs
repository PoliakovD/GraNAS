using System.Security.Cryptography;

namespace GraNAS.Desktop.App.Services.P2P;

public sealed class EcdhSession : IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly ECDiffieHellman _ecdh;
    private AesGcm? _aesGcm;

    public EcdhSession()
    {
        _ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    }

    public string GetPublicKeyBase64()
        => Convert.ToBase64String(_ecdh.PublicKey.ExportSubjectPublicKeyInfo());

    public void DeriveSharedKey(string peerPublicKeyBase64)
    {
        var peerKeyBytes = Convert.FromBase64String(peerPublicKeyBase64);
        using var peerEcdh = ECDiffieHellman.Create();
        peerEcdh.ImportSubjectPublicKeyInfo(peerKeyBytes, out _);

        var sharedSecret = _ecdh.DeriveKeyMaterial(peerEcdh.PublicKey);
        // HKDF to derive 32-byte AES-256 key
        var aesKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, null, null);
        _aesGcm = new AesGcm(aesKey, TagSize);
    }

    public bool IsReady => _aesGcm is not null;

    // Returns nonce+ciphertext+tag as a single byte array: [12 nonce][N ciphertext][16 tag]
    public byte[] Encrypt(byte[] plaintext)
    {
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        _aesGcm!.Encrypt(nonce, plaintext, ciphertext, tag);

        // Pack: nonce || ciphertext || tag
        var result = new byte[NonceSize + plaintext.Length + TagSize];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, NonceSize);
        tag.CopyTo(result, NonceSize + plaintext.Length);
        return result;
    }

    // Returns the nonce as base64 for inclusion in file_header message
    public (byte[] Encrypted, string IvBase64) EncryptWithIv(byte[] plaintext)
    {
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        _aesGcm!.Encrypt(nonce, plaintext, ciphertext, tag);

        // Pack ciphertext || tag for the binary payload
        var payload = new byte[plaintext.Length + TagSize];
        ciphertext.CopyTo(payload, 0);
        tag.CopyTo(payload, plaintext.Length);
        return (payload, Convert.ToBase64String(nonce));
    }

    public void Dispose()
    {
        _ecdh.Dispose();
        _aesGcm?.Dispose();
    }
}
