using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using FirebirdTraceAnalyzer.Interfaces;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Доставка жестов из реестра в элементы меню. Attached property <see cref="ActionProperty"/>
/// связывает MenuItem с id действия («App.OpenLocalFile», «Report.Run:&lt;id&gt;»); applier —
/// ЕДИНСТВЕННЫЙ писатель InputGesture и HotKey (шаблон Fluent показывает в меню InputGesture,
/// а не HotKey — писать надо оба, иначе текст жеста устаревает после перепривязки).
/// <para>
/// Политика macOS: in-window пункты получают ТОЛЬКО InputGesture (отображение), функциональный
/// HotKey = null — иначе ⌘-жест сработает дважды: KeyBinding окна + ускоритель нативного меню.
/// Единственный писатель HotKey — нативное меню. Смена HotKey выполняется null-first:
/// прямая замена A→B плодит мёртвые HotkeyManager и дубли KeyBinding в TopLevel.
/// </para>
/// </summary>
public static class Shortcut
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly List<WeakReference<MenuItem>> LiveItems = [];

    private static IShortcutRegistry? _registry;
    private static bool _isMac;
    private static bool _suspended;

    /// <summary>Id действия из реестра (XAML: hk:Shortcut.Action="App.OpenLocalFile").</summary>
    public static readonly AttachedProperty<string?> ActionProperty =
        AvaloniaProperty.RegisterAttached<MenuItem, string?>("Action", typeof(Shortcut));

    /// <summary>
    /// Нативное меню macOS подписывается, чтобы на время записи сочетаний снять и свои
    /// ускорители (иначе ⌘-ускоритель сработает на уровне ОС до перехвата клавиши).
    /// </summary>
    public static event EventHandler? SuspensionChanged;

    /// <summary>true — все функциональные биндинги сняты (режим записи сочетания в настройках).</summary>
    public static bool IsSuspended => _suspended;

    static Shortcut()
    {
        ActionProperty.Changed.AddClassHandler<MenuItem>(OnActionChanged);
    }

    /// <summary>
    /// Подключает реестр. Вызывается ОДИН раз из Program.ConfigureServices ДО построения
    /// MainWindow: attached property сеет жесты при построении контролов.
    /// </summary>
    public static void Initialize(IShortcutRegistry registry)
    {
        _registry = registry;
        _isMac = registry.Platform == ShortcutPlatform.MacOS;
        registry.Changed += (_, _) => RefreshAll();
    }

    /// <summary>
    /// Приостанавливает функциональные биндинги (HotKey у in-window пунктов, ускорители
    /// нативного меню через <see cref="SuspensionChanged"/>), сохраняя отображение InputGesture.
    /// Нужен в режиме записи сочетания: иначе нажатие жеста, который сейчас привязан к другому
    /// действию, ВЫПОЛНИТ это действие (KeyBinding'и проходят раньше routed KeyDown, а нативные
    /// ускорители — на уровне ОС), и записывающий пользователь получит файловый диалог вместо
    /// записи. Вызовите с true при начале записи и с false при выходе из неё.
    /// </summary>
    public static void SetSuspended(bool suspended)
    {
        if (_suspended == suspended)
            return;

        _suspended = suspended;
        RefreshAll();
        SuspensionChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string? GetAction(MenuItem item) => item.GetValue(ActionProperty);

    public static void SetAction(MenuItem item, string? actionId) => item.SetValue(ActionProperty, actionId);

    private static void OnActionChanged(MenuItem item, AvaloniaPropertyChangedEventArgs e)
    {
        Track(item);
        Apply(item, e.NewValue as string);
    }

    private static void Track(MenuItem item)
    {
        // Подчистка мёртвых ссылок: меню перестраиваются (нативное — на каждый NeedsUpdate).
        LiveItems.RemoveAll(wr => !wr.TryGetTarget(out _));

        foreach (var wr in LiveItems)
            if (wr.TryGetTarget(out var existing) && ReferenceEquals(existing, item))
                return;

        LiveItems.Add(new WeakReference<MenuItem>(item));
    }

    private static void Apply(MenuItem item, string? actionId)
    {
        var gesture = actionId is null ? null : _registry?.Resolve(actionId);

        // InputGesture — всегда (отображение), даже в режиме записи.
        item.InputGesture = gesture;

        if (_isMac || _suspended)
        {
            // macOS: ускоряет только нативное меню — окну HotKey не отдаём вообще.
            // Режим записи: снято всё функциональное (см. SetSuspended).
            item.HotKey = null;
            return;
        }

        // null-first: прямой перевод HotKey A→B оставляет старый HotkeyManager живым
        // и накапливает дубликаты KeyBinding.
        item.HotKey = null;

        if (!_suspended && gesture is not null)
            item.HotKey = gesture;
    }

    private static void RefreshAll()
    {
        foreach (var wr in LiveItems)
            if (wr.TryGetTarget(out var item))
                Apply(item, GetAction(item));
    }
}

/// <summary>
/// Конвертер Id шаблона отчёта в id действия реестра — для стилей ItemsSource-пунктов меню
/// (сгенерированные контейнеры не могут задать attached property строковым литералом).
/// </summary>
public sealed class ReportActionIdConverter : IValueConverter
{
    public static readonly ReportActionIdConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is string templateId ? ReportShortcutSource.ActionIdFor(templateId) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
