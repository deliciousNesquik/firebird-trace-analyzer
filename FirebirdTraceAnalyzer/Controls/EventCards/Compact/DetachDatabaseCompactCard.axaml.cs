using Avalonia;
using FirebirdTraceAnalyzer.Controls.EventCards;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// Компактная (однострочная) карточка события отключения от БД.
/// </summary>
public class DetachDatabaseCompactCard : ConnectedEventCardBase
{
    public static readonly StyledProperty<TimeSpan?> DurationProperty =
        AvaloniaProperty.Register<DetachDatabaseCompactCard, TimeSpan?>(nameof(Duration));

    /// <summary>Время жизни подключения (может быть null, если attach нет в файле).</summary>
    public TimeSpan? Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }
}
