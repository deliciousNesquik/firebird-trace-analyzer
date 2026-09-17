using Avalonia;
using FirebirdTraceAnalyzer.Controls.EventCards;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// База компактных карточек хранимых процедур. Помимо идентификатора подключения держит имя процедуры.
/// </summary>
public abstract class ProcedureCompactCardBase : ConnectedEventCardBase
{
    public static readonly StyledProperty<string> ProcedureNameProperty =
        AvaloniaProperty.Register<ProcedureCompactCardBase, string>(nameof(ProcedureName), string.Empty);

    public string ProcedureName
    {
        get => GetValue(ProcedureNameProperty);
        set => SetValue(ProcedureNameProperty, value);
    }
}
