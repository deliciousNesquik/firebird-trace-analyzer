using CommunityToolkit.Mvvm.ComponentModel;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Один вид фоновой задачи в индикаторе (напр. «Запись в хранилище»). <see cref="Count"/> —
/// сколько операций этого вида сейчас в работе (для очередей: растёт при постановке, падает при
/// завершении; пункт исчезает при нуле).
/// </summary>
public sealed partial class BackgroundTaskItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Title { get; init; }

    [ObservableProperty] private string? _detail;
    [ObservableProperty] private int _count;
}
