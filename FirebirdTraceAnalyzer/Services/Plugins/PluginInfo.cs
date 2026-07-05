using FirebirdTraceAnalyzer.Interfaces.Plugins;

namespace FirebirdTraceAnalyzer.Services.Plugins;

/// <summary>Что предоставляет плагин.</summary>
[Flags]
public enum PluginKind
{
    None = 0,
    Sort = 1,
    Filter = 2
}

/// <summary>Состояние плагина после загрузки и разрешения версий.</summary>
public enum PluginStatus
{
    /// <summary>Активен — его сортировки/фильтры регистрируются.</summary>
    Active,

    /// <summary>Выключен пользователем (не регистрируется). Применяется после перезапуска.</summary>
    Disabled,

    /// <summary>Затенён более новой версией плагина с тем же Id (не регистрируется).</summary>
    Shadowed,

    /// <summary>Сборку не удалось загрузить (см. <see cref="PluginInfo.LoadError"/>).</summary>
    LoadError
}

/// <summary>
/// Полная информация об обнаруженном плагине для окна управления: метаданные, путь к DLL,
/// что предоставляет, текущее состояние. Для успешно загруженных содержит рабочий экземпляр.
/// </summary>
public sealed class PluginInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Author { get; init; }
    public required string Version { get; init; }

    /// <summary>Абсолютный путь к DLL плагина.</summary>
    public required string FilePath { get; init; }

    /// <summary>Имя подпапки плагина в каталоге Plugins.</summary>
    public string DirectoryName => Path.GetFileName(Path.GetDirectoryName(FilePath) ?? string.Empty);

    /// <summary>Что предоставляет (сортировки/фильтры).</summary>
    public PluginKind Kind { get; init; }

    /// <summary>Текущее состояние (активен/выключен/затенён/ошибка).</summary>
    public PluginStatus Status { get; set; }

    /// <summary>Текст ошибки загрузки (только для <see cref="PluginStatus.LoadError"/>).</summary>
    public string? LoadError { get; init; }

    /// <summary>Разобранная версия для сравнения (null, если не распарсилась).</summary>
    public Version? ParsedVersion { get; init; }

    /// <summary>Рабочий экземпляр плагина (null при ошибке загрузки).</summary>
    public IAnalyzerPlugin? Instance { get; init; }
}
