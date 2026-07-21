namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Хранилище доверенных ключей SSH-хостов (аналог <c>known_hosts</c>).
/// Реализует TOFU (trust on first use): первый увиденный ключ хоста запоминается,
/// при последующем несовпадении отпечатка соединение должно быть отклонено (защита от MITM).
/// </summary>
public interface IHostKeyStore
{
    /// <summary>
    /// Проверяет отпечаток ключа хоста.
    /// </summary>
    /// <param name="host">Имя/адрес хоста.</param>
    /// <param name="port">Порт.</param>
    /// <param name="keyName">Тип ключа (например, <c>ssh-ed25519</c>).</param>
    /// <param name="fingerprintSha256">SHA-256 отпечаток ключа (base64).</param>
    /// <returns>
    /// <c>true</c>, если ключ ранее не встречался (запоминается) либо совпал с сохранённым;
    /// <c>false</c>, если для этого хоста уже сохранён ДРУГОЙ отпечаток (возможная подмена сервера).
    /// </returns>
    bool Verify(string host, int port, string keyName, string fingerprintSha256);
}
