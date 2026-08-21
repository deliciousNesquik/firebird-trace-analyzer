using Avalonia;
using Avalonia.Media;

namespace FirebirdTraceAnalyzer.Controls.Common;

// То же, что ButtonWithBadge, но в кружке-бейдже показывается число (напр. счётчик 1..99).
// Наследуемся, чтобы переиспользовать все свойства бейджа/размеров; добавляем только само число
// и оформление текста. Свой шаблон — в ButtonWithNumericBadge.axaml.
public class ButtonWithNumericBadge : ButtonWithBadge
{
    public static readonly StyledProperty<int> BadgeNumberProperty =
        AvaloniaProperty.Register<ButtonWithNumericBadge, int>(nameof(BadgeNumber));

    public static readonly StyledProperty<IBrush?> BadgeForegroundProperty =
        AvaloniaProperty.Register<ButtonWithNumericBadge, IBrush?>(nameof(BadgeForeground));

    public static readonly StyledProperty<double> BadgeFontSizeProperty =
        AvaloniaProperty.Register<ButtonWithNumericBadge, double>(nameof(BadgeFontSize), 10d);

    /// <summary>Gets or sets the number shown inside the badge.</summary>
    public int BadgeNumber
    {
        get => GetValue(BadgeNumberProperty);
        set => SetValue(BadgeNumberProperty, value);
    }

    /// <summary>Gets or sets the <see cref="IBrush"/> used to paint the badge text.</summary>
    public IBrush? BadgeForeground
    {
        get => GetValue(BadgeForegroundProperty);
        set => SetValue(BadgeForegroundProperty, value);
    }

    /// <summary>Gets or sets the font size of the badge text.</summary>
    public double BadgeFontSize
    {
        get => GetValue(BadgeFontSizeProperty);
        set => SetValue(BadgeFontSizeProperty, value);
    }
}
