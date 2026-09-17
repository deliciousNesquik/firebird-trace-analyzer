using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>Компактная строка ошибочного завершения процедуры (метрика — "failed" [+ время, мс]).</summary>
public class FailedProcedureFinishCompactCard : ProcedureCompactCardBase
{
    public static readonly StyledProperty<int> ExecuteMsProperty =
        AvaloniaProperty.Register<FailedProcedureFinishCompactCard, int>(nameof(ExecuteMs), 0);

    public int ExecuteMs
    {
        get => GetValue(ExecuteMsProperty);
        set => SetValue(ExecuteMsProperty, value);
    }
}
