using FirebirdTraceAnalyzer.Localization;

namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Действие, на которое можно назначить сочетание клавиш.
/// Статичные действия приложения получают заголовок по ключу локализации (<see cref="TitleLocKey"/>),
/// динамические (отчёты, в будущем плагины) — пользовательский текст <see cref="Title"/> как есть.
/// </summary>
public sealed record ShortcutAction
{
    /// <summary>Стабильный id: «App.OpenLocalFile», «Search.Focus», «Report.Run:&lt;templateId&gt;».</summary>
    public required string Id { get; init; }

    /// <summary>Ключ локализации заголовка (для статичных действий).</summary>
    public string? TitleLocKey { get; init; }

    /// <summary>Заголовок как есть (для динамических действий: имя отчёта — НЕ ключ локализации).</summary>
    public string? Title { get; init; }

    /// <summary>Ключ локализации категории (заголовок группы на странице настроек).</summary>
    public required string CategoryLocKey { get; init; }

    /// <summary>Жест по умолчанию (каноничная строка); null = действие без дефолтного биндинга.</summary>
    public string? DefaultGesture { get; init; }

    /// <summary>false — системная конвенция (⌘, в меню приложения macOS), перепривязка запрещена.</summary>
    public bool IsRebindable { get; init; } = true;

    /// <summary>Владелец: "core" (каталог ядра), "reports" (шаблоны отчётов), в будущем — ключ плагина.</summary>
    public required string Owner { get; init; }

    /// <summary>Заголовок для UI: локализованный или пользовательский текст.</summary>
    public string DisplayName => TitleLocKey is not null ? Loc.Tr(TitleLocKey) : (Title ?? Id);
}
