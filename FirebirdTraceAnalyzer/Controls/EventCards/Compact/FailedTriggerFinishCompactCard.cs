using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>Компактная строка ошибочного завершения триггера (метрика — "failed" [+ время, мс]).</summary>
public class FailedTriggerFinishCompactCard : TriggerCompactCardBase
{
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<FailedTriggerFinishCompactCard, int>(nameof(ExecuteMs), 0);

    public int ExecuteMs
    {
        get => GetValue(ExecuteMsProperty);
        set => SetValue(ExecuteMsProperty, value);
    }
}
