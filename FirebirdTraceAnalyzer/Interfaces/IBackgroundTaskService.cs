using System.Collections.ObjectModel;
using System.ComponentModel;
using FirebirdTraceAnalyzer.Models;

namespace FirebirdTraceAnalyzer.Interfaces;

/// <summary>
/// Реестр видимых фоновых задач. Немодальная мини-панель в главном окне показывает, что идёт
/// фоновая работа (напр. запись в хранилище), не мешая основной работе. Переиспользуемо: любую
/// фоновую операцию можно отметить одним <see cref="Begin"/>.
/// </summary>
public interface IBackgroundTaskService : INotifyPropertyChanged
{
    /// <summary>Активные задачи (для биндинга списка в панели).</summary>
    ReadOnlyObservableCollection<BackgroundTaskItem> Items { get; }

    /// <summary>Есть ли активные фоновые задачи (для видимости панели и предупреждения при закрытии).</summary>
    bool HasActive { get; }

    /// <summary>
    /// Отмечает начало фоновой операции. Повторный вызов с тем же <paramref name="key"/> объединяется в
    /// один пункт со счётчиком (для очередей — напр. пакет записей файлов). <see cref="IDisposable.Dispose"/>
    /// отмечает завершение; когда счётчик достигает нуля, пункт исчезает.
    /// Потокобезопасно: можно звать с любого потока, обновления UI маршалятся на UI-поток.
    /// </summary>
    IDisposable Begin(string key, string title, string? detail = null);
}
