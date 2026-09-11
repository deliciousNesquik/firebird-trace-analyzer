using System.Collections.ObjectModel;
using System.ComponentModel;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Тост-уведомления снизу-справа: короткие сообщения о результате действия, которые иначе видны
/// только в логе. Успех авто-скрывается; предупреждение/ошибка висят до закрытия пользователем.
/// Потокобезопасно: обновления UI маршалятся на UI-поток.
/// </summary>
public interface IToastService : INotifyPropertyChanged
{
    /// <summary>Видимые тосты (только для чтения; наблюдаема панелью <c>ToastHost</c>).</summary>
    ReadOnlyObservableCollection<ToastItem> Items { get; }

    /// <summary>Есть ли хоть один тост (для показа/скрытия панели).</summary>
    bool HasAny { get; }

    /// <summary>Показать тост успеха (авто-скрытие через несколько секунд).</summary>
    void Success(string title, string? message = null);

    /// <summary>Показать предупреждение (висит до закрытия).</summary>
    void Warning(string title, string? message = null);

    /// <summary>Показать ошибку (висит до закрытия).</summary>
    void Error(string title, string? message = null);

    /// <summary>Показать тост произвольного типа.</summary>
    void Show(ToastType type, string title, string? message = null);

    /// <summary>Закрыть конкретный тост (с анимацией выезда).</summary>
    void Dismiss(ToastItem item);
}
