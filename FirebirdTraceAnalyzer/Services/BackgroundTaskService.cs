using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Реестр фоновых задач. Все мутации коллекции/пунктов выполняются на UI-потоке (маршалинг через
/// <see cref="Dispatcher"/>), поэтому словарь трогается только с UI-потока и блокировки не нужны.
/// </summary>
public sealed partial class BackgroundTaskService : ObservableObject, IBackgroundTaskService
{
    private readonly ObservableCollection<BackgroundTaskItem> _items = [];
    private readonly Dictionary<string, BackgroundTaskItem> _byKey = new(StringComparer.Ordinal);

    public ReadOnlyObservableCollection<BackgroundTaskItem> Items { get; }

    [ObservableProperty] private bool _hasActive;

    public BackgroundTaskService() => Items = new ReadOnlyObservableCollection<BackgroundTaskItem>(_items);

    public IDisposable Begin(string key, string title, string? detail = null)
    {
        OnUi(() =>
        {
            if (_byKey.TryGetValue(key, out var item))
            {
                item.Count++;
                if (detail is not null) item.Detail = detail;
            }
            else
            {
                item = new BackgroundTaskItem { Key = key, Title = title, Detail = detail, Count = 1 };
                _byKey[key] = item;
                _items.Add(item);
                HasActive = true;
            }
        });

        return new Handle(this, key);
    }

    private void End(string key)
    {
        OnUi(() =>
        {
            if (!_byKey.TryGetValue(key, out var item))
                return;

            item.Count--;
            if (item.Count > 0)
                return;

            _byKey.Remove(key);
            _items.Remove(item);
            HasActive = _items.Count > 0;
        });
    }

    private static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    /// <summary>Одноразовый хэндл: первый Dispose уменьшает счётчик, повторные — игнорируются.</summary>
    private sealed class Handle(BackgroundTaskService service, string key) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                service.End(key);
        }
    }
}
