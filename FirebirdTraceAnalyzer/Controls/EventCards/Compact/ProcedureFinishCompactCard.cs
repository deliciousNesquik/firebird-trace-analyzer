using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>Компактная строка успешного завершения процедуры (метрика — время выполнения, мс).</summary>
public class ProcedureFinishCompactCard : ProcedureCompactCardBase
{
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<ProcedureFinishCompactCard, int>(nameof(ExecuteMs), 0);

    public int ExecuteMs
    {
        get => GetValue(ExecuteMsProperty);
        set => SetValue(ExecuteMsProperty, value);
    }
}
