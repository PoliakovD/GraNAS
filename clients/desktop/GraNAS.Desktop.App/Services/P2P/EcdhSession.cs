using System.Security.Cryptography;

namespace GraNAS.Desktop.App.Services.P2P;

/// <summary>
/// Управляет ECDH-рукопожатием и AES-GCM шифрованием данных P2P-сессии.
/// </summary>
/// <remarks>
/// Алгоритм:
/// <list type="number">
/// <item>Owner и receiver обмениваются публичными ключами ECDH P-256 (SPKI, Base64) через data channel.</item>
/// <item>Общий секрет — X-координата точки ECDH — извлекается через <c>DeriveRawSecretAgreement</c>.</item>
/// <item>Из секрета через HKDF-SHA256 вычисляется 256-битный ключ AES-GCM.</item>
/// </list>
/// Критически важно: используется <c>DeriveRawSecretAgreement</c>, а НЕ <c>DeriveKeyMaterial</c>.
/// На Windows <c>DeriveKeyMaterial</c> перед извлечением хеширует X через SHA-256,
/// что нарушает совместимость с WebCrypto <c>deriveBits</c> на стороне receiver.
/// </remarks>
public sealed class EcdhSession : IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly ECDiffieHellman _ecdh;
    private AesGcm? _aesGcm;

    /// <summary>Создаёт новую ECDH-сессию с генерацией пары ключей P-256.</summary>
    public EcdhSession()
    {
        _ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    }

    /// <summary>
    /// Возвращает публичный ключ этого участника в формате SPKI, Base64.
    /// Отправляется партнёру в сообщении <c>ecdh_offer</c> или <c>ecdh_answer</c>.
    /// </summary>
    public string GetPublicKeyBase64()
        => Convert.ToBase64String(_ecdh.PublicKey.ExportSubjectPublicKeyInfo());

    /// <summary>
    /// Вычисляет общий AES-GCM ключ из публичного ключа партнёра.
    /// После вызова сессия становится готовой к шифрованию (<see cref="IsReady"/> = <c>true</c>).
    /// </summary>
    /// <param name="peerPublicKeyBase64">Публичный ключ партнёра (SPKI, Base64), полученный из <c>ecdh_offer/answer</c>.</param>
    public void DeriveSharedKey(string peerPublicKeyBase64)
    {
        var peerKeyBytes = Convert.FromBase64String(peerPublicKeyBase64);
        using var peerEcdh = ECDiffieHellman.Create();
        peerEcdh.ImportSubjectPublicKeyInfo(peerKeyBytes, out _);

        // DeriveRawSecretAgreement возвращает сырую X-координату — то же, что WebCrypto deriveBits.
        // DeriveKeyMaterial на Windows дополнительно хеширует X через SHA-256 перед возвратом,
        // что ломает совместимость с receiver.
        var rawSecret = _ecdh.DeriveRawSecretAgreement(peerEcdh.PublicKey);
        var aesKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, rawSecret, 32, [], []);
        _aesGcm = new AesGcm(aesKey, TagSize);
    }

    /// <summary><c>true</c>, если ECDH-рукопожатие завершено и сессия готова к шифрованию.</summary>
    public bool IsReady => _aesGcm is not null;

    /// <summary>
    /// Шифрует блок данных с AES-GCM и случайным nonce.
    /// </summary>
    /// <param name="plaintext">Открытый текст.</param>
    /// <returns>Бинарный фрейм в формате <c>nonce(12) || ciphertext(N) || tag(16)</c>.</returns>
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

    /// <summary>
    /// Шифрует данные и возвращает payload и nonce отдельно.
    /// Используется при формировании <c>file_header</c>, когда nonce нужен отдельно в виде Base64.
    /// </summary>
    /// <returns>Кортеж: <c>Encrypted</c> — <c>ciphertext || tag(16)</c>; <c>IvBase64</c> — nonce в Base64.</returns>
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
