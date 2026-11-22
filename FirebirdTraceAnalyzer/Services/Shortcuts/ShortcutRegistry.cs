using System.Runtime.InteropServices;
using Avalonia.Input;
using FirebirdTraceAnalyzer.Interfaces;

namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Реализация реестра горячих клавиш: объединение действий источников + оверрайды.
/// Платформа инжектится (тесты детерминированы на любом CI), по умолчанию — по ОС.
/// </summary>
public sealed class ShortcutRegistry : IShortcutRegistry
{
    private readonly List<IShortcutActionSource> _sources = [];
    private readonly Dictionary<string, string?> _overrides = new(StringComparer.Ordinal);
    private readonly List<string> _loadWarnings = [];

    // Кеш объединения действий: источники поднимают Changed редко, снимки короткие.
    private IReadOnlyList<ShortcutAction>? _actionsCache;
    private bool _actionsDirty = true;

    public ShortcutRegistry(ShortcutPlatform? platform = null)
    {
        Platform = platform ?? DetectPlatform();

        // Каталог — обычный источник: один путь наполнения для всех владельцев.
        AddSource(new StaticShortcutSource(ShortcutCatalog.CoreActions));
    }

    public ShortcutPlatform Platform { get; }

    public event EventHandler? Changed;

    public IReadOnlyList<ShortcutAction> Actions
    {
        get
        {
            if (!_actionsDirty)
                return _actionsCache!;

            var actions = new List<ShortcutAction>();

            foreach (var source in _sources)
                actions.AddRange(source.Snapshot());

            _actionsCache = actions;
            _actionsDirty = false;
            return actions;
        }
    }

    public ShortcutAction? FindAction(string actionId) =>
        Actions.FirstOrDefault(a => string.Equals(a.Id, actionId, StringComparison.Ordinal));

    public GestureSpec? ResolveSpec(string actionId)
    {
        var action = FindAction(actionId);

        if (action is null)
            return null;

        // Оверрайд: null = осознанно отключено; некорректная строка → откат на дефолт.
        if (_overrides.TryGetValue(actionId, out var raw))
        {
            if (raw is null)
                return null;

            if (GestureParser.TryParse(raw, out var overrideSpec, out _))
                return overrideSpec;
        }

        if (action.DefaultGesture is null)
            return null;

        return GestureParser.TryParse(action.DefaultGesture, out var defaultSpec, out _)
            ? defaultSpec
            : null;
    }

    public KeyGesture? Resolve(string actionId) =>
        ResolveSpec(actionId) is { } spec ? GestureParser.ToKeyGesture(spec, Platform) : null;

    public void AddSource(IShortcutActionSource source)
    {
        // Заменяем существующий источник того же владельца: реестр — синглтон, а владельцы
        // (MainWindowViewModel, в будущем PluginManagerService) могут пересоздаваться.
        // Без этого каждый пересозданный VM добавил бы дубликаты всех Report.Run:* действий.
        _sources.RemoveAll(s => string.Equals(s.Owner, source.Owner, StringComparison.Ordinal));
        _sources.Add(source);
        _actionsDirty = true;
        RaiseChanged();
        source.Changed += (_, _) =>
        {
            _actionsDirty = true;
            RaiseChanged();
        };
    }

    public IReadOnlyDictionary<string, string?> Overrides => _overrides;

    public IReadOnlyList<string> LoadWarnings => _loadWarnings;

    public void LoadOverrides(IReadOnlyDictionary<string, string?> entries)
    {
        _overrides.Clear();
        _loadWarnings.Clear();

        // Один жест не может принадлежать двум действиям: при ручной правке файла проигравший
        // (позже по порядку вставки) отбрасывается с предупреждением, иначе оба KeyBinding
        // сосуществуют и срабатывает «первый в дереве» — поведение незаметно и непредсказуемо.
        var claimedGestures = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (actionId, raw) in entries)
        {
            if (FindAction(actionId) is null)
            {
                // Осиротевший (удалённый отчёт) или неизвестный id: сохраняем в файле — отчёт
                // может вернуться с тем же GUID; разрешаться он не будет.
                _overrides[actionId] = raw;
                _loadWarnings.Add($"Unknown action id '{actionId}' — override retained, not applied");
                continue;
            }

            if (raw is null)
            {
                _overrides[actionId] = null;
                continue;
            }

            if (!GestureParser.TryParse(raw, out var spec, out var parseError))
            {
                _loadWarnings.Add($"Invalid gesture '{raw}' for '{actionId}': {parseError} — default applies");
                continue;
            }

            var canonical = spec.ToString();

            if (claimedGestures.TryGetValue(canonical, out var winnerId))
            {
                _loadWarnings.Add(
                    $"Gesture '{canonical}' already assigned to '{winnerId}' — override for '{actionId}' dropped");
                continue;
            }

            _overrides[actionId] = canonical;
            claimedGestures[canonical] = actionId;
        }

        // Физическая проверка поверх канонической: оверрайд может лечь на ЧУЖОЙ дефолт
        // («Primary+O» на App.ToggleFilesSection при дефолтном Primary+O у OpenLocalFile) —
        // каноничные строки при этом разные, а физический жест один. Оверрайды не мутируем
        // (файл — пользовательские данные), фиксируем предупреждение: иначе побеждал бы
        // молча «первый в дереве».
        var physicallyClaimed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var action in Actions)
        {
            if (Resolve(action.Id) is not { } gesture)
                continue;

            var physical = $"{gesture.Key}+{gesture.KeyModifiers}";

            if (physicallyClaimed.TryGetValue(physical, out var winner))
                _loadWarnings.Add(
                    $"Physical gesture '{physical}' resolves for both '{winner}' and '{action.Id}' — menu tree order wins");
            else
                physicallyClaimed[physical] = action.Id;
        }

        RaiseChanged();
    }

    public bool SetOverride(string actionId, string? canonicalGesture)
    {
        var action = FindAction(actionId);

        if (action is null || !action.IsRebindable)
            return false;

        if (canonicalGesture is not null)
        {
            if (!GestureParser.TryParse(canonicalGesture, out var spec, out _))
                return false;

            canonicalGesture = spec.ToString();
        }

        _overrides[actionId] = canonicalGesture;
        RaiseChanged();
        return true;
    }

    public void ClearOverride(string actionId)
    {
        if (_overrides.Remove(actionId))
            RaiseChanged();
    }

    public void ResetAll()
    {
        if (_overrides.Count == 0)
            return;

        _overrides.Clear();
        RaiseChanged();
    }

    public ShortcutAction? FindConflicting(string canonicalGesture, string excludeActionId)
    {
        // Сравниваем ФИЗИЧЕСКИ разрешённые жесты, а не каноничные строки: коллизия в рантайме —
        // на конкретной платформе («Ctrl+Shift+O» и «Primary+Shift+O» на Windows — один и тот же
        // Control+Shift+O, канонично они разные, поскольку Ctrl — осознанный физический выбор).
        if (!GestureParser.TryParse(canonicalGesture, out var spec, out _))
            return null;

        var target = GestureParser.ToKeyGesture(spec, Platform);

        if (target is null)
            return null;

        return Actions.FirstOrDefault(action =>
        {
            if (action.Id == excludeActionId)
                return false;

            var other = Resolve(action.Id);
            return other is not null
                   && other.Key == target.Key
                   && other.KeyModifiers == target.KeyModifiers;
        });
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static ShortcutPlatform DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ShortcutPlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ShortcutPlatform.MacOS;

        return ShortcutPlatform.Linux;
    }
}
