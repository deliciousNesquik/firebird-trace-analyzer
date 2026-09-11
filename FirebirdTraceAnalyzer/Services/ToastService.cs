using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Services;

/// <summary>
/// Реестр тост-уведомлений. Все мутации коллекции — на UI-потоке (маршалинг через <see cref="Dispatcher"/>),
/// поэтому блокировки не нужны. Анимация въезда/выезда живёт в <c>ToastHost</c> и управляется флагом
/// <see cref="ToastItem.IsShown"/>: сюда добавляем как скрытый и на следующем кадре показываем; на закрытии
/// снимаем показ и убираем из коллекции после короткой задержки (равной длительности анимации выезда).
/// </summary>
public sealed partial class ToastService : ObservableObject, IToastService
{
    // Успех сам исчезает; предупреждение/ошибка — только вручную (крестиком).
    private static readonly TimeSpan SuccessLifetime = TimeSpan.FromSeconds(3.5);
    // Должно совпадать с длительностью анимации выезда в ToastHost.axaml (0.22с) + небольшой запас.
    private static readonly TimeSpan ExitAnimation = TimeSpan.FromMilliseconds(260);

    private readonly ObservableCollection<ToastItem> _items = [];

    public ReadOnlyObservableCollection<ToastItem> Items { get; }

    [ObservableProperty] private bool _hasAny;

    public ToastService() => Items = new ReadOnlyObservableCollection<ToastItem>(_items);

    public void Success(string title, string? message = null) => Show(ToastType.Success, title, message);
    public void Warning(string title, string? message = null) => Show(ToastType.Warning, title, message);
    public void Error(string title, string? message = null) => Show(ToastType.Error, title, message);

    public void Show(ToastType type, string title, string? message = null)
    {
        OnUi(() =>
        {
            var item = new ToastItem
            {
                Id = Guid.NewGuid(),
                Type = type,
                Title = title,
                Message = message,
                IsShown = false, // стартовое скрытое состояние — анимация въезда сыграет на след. кадре
            };

            _items.Add(item);
            HasAny = true;

            // Следующий кадр: показываем -> стили проигрывают въезд справа.
            Dispatcher.UIThread.Post(() => item.IsShown = true, DispatcherPriority.Render);

            // Успех гасим сами; предупреждение/ошибка ждут крестика.
            if (type == ToastType.Success)
                DispatcherTimer.RunOnce(() => Dismiss(item), SuccessLifetime);
        });
    }

    public void Dismiss(ToastItem item)
    {
        OnUi(() =>
        {
            if (!_items.Contains(item))
                return;

            item.IsShown = false; // проигрываем выезд
            DispatcherTimer.RunOnce(() =>
            {
                _items.Remove(item);
                HasAny = _items.Count > 0;
            }, ExitAnimation);
        });
    }

    private static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
