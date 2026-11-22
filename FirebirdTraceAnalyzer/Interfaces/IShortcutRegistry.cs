using Avalonia.Input;
using FirebirdTraceAnalyzer.Services.Shortcuts;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Реестр горячих клавиш: объединение действий всех источников + пользовательские оверрайды.
/// Оверрайды хранятся как каноничные строки жестов или null (= действие осознанно отключено);
/// отсутствие записи означает дефолт из действия. Изменения уведомляют подписчиков (applier,
/// нативное меню) через <see cref="Changed"/>.
/// </summary>
public interface IShortcutRegistry
{
    /// <summary>Платформа, для которой разрешаются жесты (Primary → Meta на macOS).</summary>
    ShortcutPlatform Platform { get; }

    /// <summary>Снимок всех действий: статичный каталог + все источники.</summary>
    IReadOnlyList<ShortcutAction> Actions { get; }

    ShortcutAction? FindAction(string actionId);

    /// <summary>
    /// Разрешённый спецификатор: оверрайд (строка → парсинг, null → отключено) либо дефолт.
    /// Некорректный оверрайд откатывает на дефолт. Неизвестный id → null.
    /// </summary>
    GestureSpec? ResolveSpec(string actionId);

    /// <summary>Физический KeyGesture для платформы; null = жест отсутствует/отключено/неизвестный id.</summary>
    KeyGesture? Resolve(string actionId);

    /// <summary>Подключает источник действий (каталог подключается при создании, отчёты — из VM).</summary>
    void AddSource(IShortcutActionSource source);

    /// <summary>Сырые оверрайды (включая неизвестные id и отключения) — ровно то, что пишется в файл.</summary>
    IReadOnlyDictionary<string, string?> Overrides { get; }

    /// <summary>
    /// Загружает оверрайды из файла. Толерантно: некорректный жест → дефолт + предупреждение;
    /// конфликт (один жест на два действия) → по порядку вставки, проигравший отбрасывается +
    /// предупреждение; неизвестный id → сохраняется в Overrides (переживает циклы записей),
    /// не разрешается, предупреждение.
    /// </summary>
    void LoadOverrides(IReadOnlyDictionary<string, string?> entries);

    /// <summary>Предупреждения последней загрузки (для лога/диагностики).</summary>
    IReadOnlyList<string> LoadWarnings { get; }

    /// <summary>
    /// Ставит оверрайд: каноничная строка или null (= отключено). Отклоняется для неизвестных id
    /// и действий с IsRebindable=false.
    /// </summary>
    bool SetOverride(string actionId, string? canonicalGesture);

    /// <summary>Убирает оверрайд — действие возвращается к дефолту.</summary>
    void ClearOverride(string actionId);

    /// <summary>Сбрасывает все оверрайды в памяти (удаление файла — ответственность вызывающего).</summary>
    void ResetAll();

    /// <summary>
    /// Ищет ДРУГОЕ действие с тем же разрешённым жестом (для превентивной конфлюкт-детекции UI).
    /// </summary>
    ShortcutAction? FindConflicting(string canonicalGesture, string excludeActionId);

    /// <summary>Любое изменение набора действий или оверрайдов.</summary>
    event EventHandler? Changed;
}
