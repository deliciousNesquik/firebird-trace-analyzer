using Avalonia;
using FirebirdTraceAnalyzer.Controls.EventCards;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// База компактных карточек SQL statement. Помимо идентификатора подключения
/// (<see cref="ConnectedEventCardBase"/>) держит текст SQL для однострочного показа.
/// </summary>
public abstract class StatementCompactCardBase : ConnectedEventCardBase
{
    public static readonly StyledProperty<string> SqlProperty =
        AvaloniaProperty.Register<StatementCompactCardBase, string>(nameof(Sql), string.Empty);

    public string Sql
    {
        get => GetValue(SqlProperty);
        set => SetValue(SqlProperty, value);
    }
}
