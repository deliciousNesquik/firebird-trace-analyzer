using FirebirdTraceAnalyzer.Services;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// TOFU-хранилище ключей хостов (C1: защита SSH от MITM). Проверяем и нормальные сценарии,
/// и абсурдные краевые: пустой отпечаток, битый файл, повторное открытие с диска.
/// </summary>
public sealed class KnownHostsStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(),
        "fta_known_hosts_test_" + Guid.NewGuid().ToString("N") + ".txt");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void FirstUse_Trusts_AndPersists()
    {
        var store = new KnownHostsStore(_path);
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "AAAAfingerprint"));
        // Второй запрос тем же ключом — доверяем.
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "AAAAfingerprint"));
        // Пережило запись на диск: новый экземпляр видит тот же ключ.
        Assert.True(new KnownHostsStore(_path).Verify("host", 22, "ssh-ed25519", "AAAAfingerprint"));
    }

    [Fact]
    public void ChangedFingerprint_IsRejected()
    {
        var store = new KnownHostsStore(_path);
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "GOOD"));
        // Подмена ключа сервера — отказ.
        Assert.False(store.Verify("host", 22, "ssh-ed25519", "EVIL"));
        // И после перезагрузки с диска подмена по-прежнему отклоняется.
        Assert.False(new KnownHostsStore(_path).Verify("host", 22, "ssh-ed25519", "EVIL"));
    }

    [Fact]
    public void EmptyFingerprint_IsRejected_WithoutPersisting()
    {
        var store = new KnownHostsStore(_path);
        Assert.False(store.Verify("host", 22, "ssh-ed25519", ""));
        Assert.False(store.Verify("host", 22, "ssh-ed25519", null!));
        // Пустой отпечаток не должен ничего запоминать: настоящий ключ потом принимается.
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "REAL"));
    }

    [Fact]
    public void DifferentHostPort_AreIndependent()
    {
        var store = new KnownHostsStore(_path);
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "A"));
        Assert.True(store.Verify("host", 2222, "ssh-ed25519", "B"));   // другой порт — независим
        Assert.True(store.Verify("other", 22, "ssh-ed25519", "C"));    // другой хост — независим
        // А исходная запись не перетёрлась.
        Assert.False(store.Verify("host", 22, "ssh-ed25519", "TAMPERED"));
    }

    [Fact]
    public void ChangedKeyType_OnKnownHost_IsRejected()
    {
        // Доверие ключуется по (host, port): смена АЛГОРИТМА на известном хосте — это MITM,
        // а не «новый хост». Иначе посредник, навязав другой тип ключа, обошёл бы проверку.
        var store = new KnownHostsStore(_path);
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "GOOD"));
        Assert.False(store.Verify("host", 22, "ssh-rsa", "ATTACKER"));           // другой тип ключа — отказ
        Assert.False(new KnownHostsStore(_path).Verify("host", 22, "ssh-rsa", "ATTACKER")); // и после перезагрузки
        // Легитимный исходный ключ по-прежнему принимается.
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "GOOD"));
    }

    [Fact]
    public void CorruptFile_DoesNotThrow_AndTofuStillWorks()
    {
        File.WriteAllText(_path, "garbage line without tabs\n\thalf\tbroken\n# comment\n\n");
        var store = new KnownHostsStore(_path);
        // Битые строки игнорируются, новый ключ доверяется по TOFU.
        Assert.True(store.Verify("host", 22, "ssh-ed25519", "X"));
    }
}
