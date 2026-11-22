namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Источник действий для реестра. Статичный каталог и динамические владельцы (отчёты,
/// в будущем плагины) кормят один и тот же пайплайн разрешения; динамизм — вопрос
/// наполнения, оверрайды/конфликты/applier про источники ничего не знают.
/// </summary>
public interface IShortcutActionSource
{
    /// <summary>Стабильный ключ владельца: "core", "reports", в будущем — ключ плагина.</summary>
    string Owner { get; }

    /// <summary>Полный снимок действий на текущий момент (без инкрементальных дельт).</summary>
    IReadOnlyList<ShortcutAction> Snapshot();

    /// <summary>Набор действий изменился — реестр пересобирает объединение.</summary>
    event EventHandler? Changed;
}
