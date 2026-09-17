using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>Компактная строка успешного завершения statement (метрика — время выполнения, мс).</summary>
public class StatementFinishCompactCard : StatementCompactCardBase
{
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<StatementFinishCompactCard, int>(nameof(ExecuteMs), 0);

    public int ExecuteMs
    {
        get => GetValue(ExecuteMsProperty);
        set => SetValue(ExecuteMsProperty, value);
    }
}
