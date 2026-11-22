using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FirebirdTraceAnalyzer.Models.Reports;

namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Источник динамических действий «запустить отчёт»: каждое действие — Report.Run:&lt;templateId&gt;,
/// заголовок — имя шаблона как есть (пользовательский текст, НЕ ключ локализации). Все отчёты
/// стартуют без дефолтного биндинга. Полный пересчёт снимка на каждое изменение коллекций:
/// элементов десятки, а RefreshCustomReportsAsync делает Clear()+refill — инкрементальное
/// слияние тут избыточно и опаснее по корректности.
/// </summary>
public sealed class ReportShortcutSource : IShortcutActionSource, IDisposable
{
    public const string ActionPrefix = "Report.Run:";

    private readonly ObservableCollection<ReportTemplate> _builtIn;
    private readonly ObservableCollection<ReportTemplate> _custom;

    public ReportShortcutSource(
        ObservableCollection<ReportTemplate> builtIn,
        ObservableCollection<ReportTemplate> custom)
    {
        _builtIn = builtIn;
        _custom = custom;
        _builtIn.CollectionChanged += OnCollectionChanged;
        _custom.CollectionChanged += OnCollectionChanged;
    }

    public string Owner => ShortcutCatalog.OwnerReports;

    public event EventHandler? Changed;

    public static string ActionIdFor(string templateId) => ActionPrefix + templateId;

    public IReadOnlyList<ShortcutAction> Snapshot() =>
        _builtIn.Concat(_custom)
            .Select(t => new ShortcutAction
            {
                Id = ActionIdFor(t.Id),
                Title = t.Name,
                CategoryLocKey = ShortcutCatalog.CategoryReports,
                DefaultGesture = null,
                Owner = ShortcutCatalog.OwnerReports,
            })
            .ToList();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _builtIn.CollectionChanged -= OnCollectionChanged;
        _custom.CollectionChanged -= OnCollectionChanged;
    }
}
