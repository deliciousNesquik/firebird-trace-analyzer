using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces.Remote;

/// <summary>
/// Хранилище сохранённых SSH-профилей подключения (JSON-файл). Инкапсулирует файловый ввод-вывод,
/// чтобы ViewModel не занималась персистентностью напрямую (SoC/тестируемость).
/// </summary>
public interface ISshProfileStore
{
    /// <summary>Путь к файлу профилей (для команды «открыть файл профилей»).</summary>
    string FilePath { get; }

    /// <summary>Читает профили с диска. При отсутствии/ошибке возвращает пустой список (не бросает).</summary>
    IReadOnlyList<SshConnectionProfile> Load();

    /// <summary>Сохраняет профили на диск (перезаписывает файл).</summary>
    Task SaveAsync(IEnumerable<SshConnectionProfile> profiles);
}
