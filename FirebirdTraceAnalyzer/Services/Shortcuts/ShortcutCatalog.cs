namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Каталог статичных действий ядра — единственный источник дефолтных жестов.
/// Дефолты живут в коде (не в appsettings.json): они неотделимы от ключей локализации
/// и категорий, а файл пользователя хранит только оверрайды.
/// </summary>
public static class ShortcutCatalog
{
    public const string OwnerCore = "core";
    public const string OwnerReports = "reports";

    public const string CategoryFiles = "Shortcuts.Category.Files";
    public const string CategoryLayout = "Shortcuts.Category.Layout";
    public const string CategorySearch = "Shortcuts.Category.Search";
    public const string CategoryApp = "Shortcuts.Category.App";
    public const string CategoryReports = "Shortcuts.Category.Reports";

    /// <summary>Статичные действия: 15 пунктов меню с жестами + Ctrl+F (фокус поиска, не пункт меню).</summary>
    public static readonly ShortcutAction[] CoreActions =
    [
        // --- Файлы ---
        new()
        {
            Id = "App.OpenLocalFile", TitleLocKey = "Shortcuts.Action.OpenLocalFile",
            CategoryLocKey = CategoryFiles, DefaultGesture = "Primary+O", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.OpenRemoteFile", TitleLocKey = "Shortcuts.Action.OpenRemoteFile",
            CategoryLocKey = CategoryFiles, DefaultGesture = "Primary+Shift+O", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.CancelLoading", TitleLocKey = "Shortcuts.Action.CancelLoading",
            CategoryLocKey = CategoryFiles, DefaultGesture = "Primary+Escape", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.ReparseAllFiles", TitleLocKey = "Shortcuts.Action.ReparseAllFiles",
            CategoryLocKey = CategoryFiles, DefaultGesture = "Primary+R", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.ReparseSelectedFiles", TitleLocKey = "Shortcuts.Action.ReparseSelectedFiles",
            CategoryLocKey = CategoryFiles, DefaultGesture = "Primary+Shift+R", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.CloseAllFiles", TitleLocKey = "Shortcuts.Action.CloseAllFiles",
            CategoryLocKey = CategoryFiles, DefaultGesture = "Primary+Shift+W", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.CloseSelectedFiles", TitleLocKey = "Shortcuts.Action.CloseSelectedFiles",
            CategoryLocKey = CategoryFiles, DefaultGesture = "Primary+W", Owner = OwnerCore,
        },

        // --- Макет ---
        new()
        {
            Id = "App.ToggleFilesSection", TitleLocKey = "Shortcuts.Action.ToggleFiles",
            CategoryLocKey = CategoryLayout, DefaultGesture = "Primary+D1", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.ToggleSearchSection", TitleLocKey = "Shortcuts.Action.ToggleSearch",
            CategoryLocKey = CategoryLayout, DefaultGesture = "Primary+D2", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.ToggleEventsSection", TitleLocKey = "Shortcuts.Action.ToggleEvents",
            CategoryLocKey = CategoryLayout, DefaultGesture = "Primary+D3", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.ToggleStatisticsSection", TitleLocKey = "Shortcuts.Action.ToggleStatistics",
            CategoryLocKey = CategoryLayout, DefaultGesture = "Primary+D4", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.ToggleLogsSection", TitleLocKey = "Shortcuts.Action.ToggleLogs",
            CategoryLocKey = CategoryLayout, DefaultGesture = "Primary+D5", Owner = OwnerCore,
        },
        new()
        {
            Id = "App.ResetLayout", TitleLocKey = "Shortcuts.Action.ResetLayout",
            CategoryLocKey = CategoryLayout, DefaultGesture = "Primary+D0", Owner = OwnerCore,
        },

        // --- Поиск ---
        new()
        {
            // Фокус в поле поиска: не пункт меню, обрабатывается в MainWindow.OnKeyDown через Matches.
            Id = "Search.Focus", TitleLocKey = "Shortcuts.Action.FocusSearch",
            CategoryLocKey = CategorySearch, DefaultGesture = "Primary+F", Owner = OwnerCore,
        },

        // --- Приложение ---
        new()
        {
            // ⌘,/Ctrl+, — системная конвенция окна настроек; пункт меню приложения macOS задаётся
            // декларативно в App.axaml и перепривязке не подлежит. Действие показываем в списке,
            // но Rebindable=false.
            Id = "App.OpenSettings", TitleLocKey = "Shortcuts.Action.OpenSettings",
            CategoryLocKey = CategoryApp, DefaultGesture = "Primary+OemComma",
            IsRebindable = false, Owner = OwnerCore,
        },
    ];
}
