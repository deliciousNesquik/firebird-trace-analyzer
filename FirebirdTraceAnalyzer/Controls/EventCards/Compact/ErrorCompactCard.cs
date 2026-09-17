using Avalonia;
using FirebirdTraceAnalyzer.Controls.EventCards;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// Компактная (однострочная) карточка ошибки Firebird: код и сообщение первой строки стека
/// плюс счётчик остальных строк ("· +N"). Полный стек и компонент — в полной карточке.
/// </summary>
public class ErrorCompactCard : EventCardBase
{
    public static readonly StyledProperty<long> AttachmentIdProperty =
        AvaloniaProperty.Register<ErrorCompactCard, long>(nameof(AttachmentId), 0);

    public static readonly StyledProperty<IReadOnlyList<ErrorLines>> ErrorsProperty =
        AvaloniaProperty.Register<ErrorCompactCard, IReadOnlyList<ErrorLines>>(
            nameof(Errors), Array.Empty<ErrorLines>());

    public long AttachmentId
    {
        get => GetValue(AttachmentIdProperty);
        set => SetValue(AttachmentIdProperty, value);
    }

    public IReadOnlyList<ErrorLines> Errors
    {
        get => GetValue(ErrorsProperty);
        set => SetValue(ErrorsProperty, value);
    }
}
