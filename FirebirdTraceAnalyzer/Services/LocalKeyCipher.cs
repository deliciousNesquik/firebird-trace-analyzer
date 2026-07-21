using System.Security.Cryptography;
using System.Text;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Симметричное шифрование строк на локальном ключе (AES-256-GCM) для фолбэк-хранилища кредов,
/// когда OS-keyring недоступен. Формат полезной нагрузки: base64( nonce[12] | tag[16] | ciphertext ).
/// AEAD (GCM) даёт и конфиденциальность, и целостность — порча/подмена данных обнаруживается на Decrypt.
/// </summary>
public static class LocalKeyCipher
{
    private static readonly int NonceLen = AesGcm.NonceByteSizes.MaxSize;
    private static readonly int TagLen = AesGcm.TagByteSizes.MaxSize;

    public static string Encrypt(byte[] key, string plaintext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceLen);
        var tag = new byte[TagLen];
        var cipher = new byte[plainBytes.Length];

        using (var aes = new AesGcm(key, TagLen))
            aes.Encrypt(nonce, plainBytes, cipher, tag);

        var payload = new byte[NonceLen + TagLen + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceLen);
        Buffer.BlockCopy(tag, 0, payload, NonceLen, TagLen);
        Buffer.BlockCopy(cipher, 0, payload, NonceLen + TagLen, cipher.Length);

        return Convert.ToBase64String(payload);
    }

    public static string Decrypt(byte[] key, string payloadBase64)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(payloadBase64);

        var payload = Convert.FromBase64String(payloadBase64);
        if (payload.Length < NonceLen + TagLen)
            throw new CryptographicException("Corrupted credential payload");

        var nonce = payload.AsSpan(0, NonceLen);
        var tag = payload.AsSpan(NonceLen, TagLen);
        var cipher = payload.AsSpan(NonceLen + TagLen);
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(key, TagLen))
            aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
