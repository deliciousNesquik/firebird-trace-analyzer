using System.Security.Cryptography;
using FirebirdTraceAnalyzer.Services;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// S5: фолбэк-шифрование кредов (AES-256-GCM). Проверяем round-trip и что порча/чужой ключ/битый
/// payload обнаруживаются, а не молча дают мусор. Плюс абсурдные входы.
/// </summary>
public sealed class LocalKeyCipherTests
{
    private static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    [Theory]
    [InlineData("simple")]
    [InlineData("")]
    [InlineData("пароль-с-юникодом-🔐-and-emoji")]
    [InlineData("with\nnewlines\tand\0nulls")]
    public void RoundTrip_RestoresExactly(string secret)
    {
        var key = NewKey();
        var payload = LocalKeyCipher.Encrypt(key, secret);
        Assert.NotEqual(secret, payload); // не открытый текст
        Assert.Equal(secret, LocalKeyCipher.Decrypt(key, payload));
    }

    [Fact]
    public void LongSecret_RoundTrips()
    {
        var key = NewKey();
        var secret = new string('x', 100_000);
        Assert.Equal(secret, LocalKeyCipher.Decrypt(key, LocalKeyCipher.Encrypt(key, secret)));
    }

    [Fact]
    public void SameSecret_ProducesDifferentCiphertext_DueToRandomNonce()
    {
        var key = NewKey();
        Assert.NotEqual(LocalKeyCipher.Encrypt(key, "x"), LocalKeyCipher.Encrypt(key, "x"));
    }

    [Fact]
    public void WrongKey_FailsAuthentication()
    {
        var payload = LocalKeyCipher.Encrypt(NewKey(), "secret");
        Assert.ThrowsAny<CryptographicException>(() => LocalKeyCipher.Decrypt(NewKey(), payload));
    }

    [Fact]
    public void TamperedPayload_IsDetected()
    {
        var key = NewKey();
        var payload = LocalKeyCipher.Encrypt(key, "secret");
        var bytes = Convert.FromBase64String(payload);
        bytes[^1] ^= 0xFF; // портим последний байт шифротекста
        var tampered = Convert.ToBase64String(bytes);
        Assert.ThrowsAny<CryptographicException>(() => LocalKeyCipher.Decrypt(key, tampered));
    }

    [Fact]
    public void TruncatedPayload_Throws()
    {
        var key = NewKey();
        Assert.Throws<CryptographicException>(() => LocalKeyCipher.Decrypt(key, Convert.ToBase64String(new byte[4])));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        var key = NewKey();
        Assert.Throws<ArgumentNullException>(() => LocalKeyCipher.Encrypt(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => LocalKeyCipher.Encrypt(key, null!));
        Assert.Throws<ArgumentNullException>(() => LocalKeyCipher.Decrypt(null!, "x"));
    }
}
