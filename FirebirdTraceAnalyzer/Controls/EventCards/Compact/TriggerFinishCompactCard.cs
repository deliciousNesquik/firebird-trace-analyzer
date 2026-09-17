using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>Компактная строка успешного завершения триггера (метрика — время выполнения, мс).</summary>
public class TriggerFinishCompactCard : TriggerCompactCardBase
{
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<TriggerFinishCompactCard, int>(nameof(ExecuteMs), 0);

    public int ExecuteMs
    {
        get => GetValue(ExecuteMsProperty);
        set => SetValue(ExecuteMsProperty, value);
    }
}
