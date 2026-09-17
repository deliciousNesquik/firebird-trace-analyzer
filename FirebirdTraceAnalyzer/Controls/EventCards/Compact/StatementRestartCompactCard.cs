using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>Компактная строка перезапуска statement (метрика — число перезапусков).</summary>
public class StatementRestartCompactCard : StatementCompactCardBase
{
    public static readonly StyledProperty<int?> RestartCountProperty =
        AvaloniaProperty.Register<StatementRestartCompactCard, int?>(nameof(RestartCount));

    public int? RestartCount
    {
        get => GetValue(RestartCountProperty);
        set => SetValue(RestartCountProperty, value);
    }
}
