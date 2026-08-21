using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace FirebirdTraceAnalyzer.Controls.Common;

public class ButtonWithBadge : ContentControl
{
    
    public static readonly StyledProperty<bool> BadgeVisibilityProperty =
        AvaloniaProperty.Register<ButtonWithBadge, bool>(nameof(BadgeVisibility));

    public static readonly StyledProperty<IBrush?> BadgeColorProperty =
        AvaloniaProperty.Register<ButtonWithBadge, IBrush?>(nameof(BadgeColor));
    
    public static readonly StyledProperty<IBrush?> BadgeOutlineProperty =
        AvaloniaProperty.Register<ButtonWithBadge, IBrush?>(nameof(BadgeOutline));
    
    public static readonly StyledProperty<double> BadgeOutlineThicknessProperty =
        AvaloniaProperty.Register<ButtonWithBadge, double>(nameof(BadgeOutlineThickness));
    
    public static readonly StyledProperty<double> BadgeWidthProperty =
        AvaloniaProperty.Register<ButtonWithBadge, double>(nameof(BadgeWidth));

    public static readonly StyledProperty<double> BadgeHeightProperty =
        AvaloniaProperty.Register<ButtonWithBadge, double>(nameof(BadgeHeight));

    public static readonly StyledProperty<double> ButtonWidthProperty =
        AvaloniaProperty.Register<ButtonWithBadge, double>(nameof(ButtonWidth));

    public static readonly StyledProperty<double> ButtonHeightProperty =
        AvaloniaProperty.Register<ButtonWithBadge, double>(nameof(ButtonHeight));

    public static readonly StyledProperty<double> TotalHeightProperty =
        AvaloniaProperty.Register<ButtonWithBadge, double>(nameof(TotalHeight));

    public static readonly StyledProperty<double> TotalWidthProperty =
        AvaloniaProperty.Register<ButtonWithBadge, double>(nameof(TotalWidth));

    public static readonly StyledProperty<HorizontalAlignment> BadgeHorizontalAlignmentProperty =
        AvaloniaProperty.Register<ButtonWithBadge, HorizontalAlignment>(nameof(BadgeHorizontalAlignment));

    public static readonly StyledProperty<VerticalAlignment> BadgeVerticalAlignmentProperty =
        AvaloniaProperty.Register<ButtonWithBadge, VerticalAlignment>(nameof(BadgeVerticalAlignment));

    public static readonly StyledProperty<FlyoutBase?> FlyoutProperty =
        AvaloniaProperty.Register<ButtonWithBadge, FlyoutBase?>(nameof(Flyout));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ButtonWithBadge, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<ButtonWithBadge, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<Thickness> BadgeMarginProperty =
        AvaloniaProperty.Register<ButtonWithBadge, Thickness>(nameof(BadgeMargin));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ButtonWidthProperty ||
            change.Property == ButtonHeightProperty ||
            change.Property == BadgeHeightProperty ||
            change.Property == BadgeWidthProperty ||
            change.Property == BadgeHorizontalAlignmentProperty ||
            change.Property == BadgeVerticalAlignmentProperty)
        {
            UpdateGridSize();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateGridSize();
    }

    private void UpdateGridSize()
    {
        TotalWidth = ButtonWidth + BadgeWidth;
        TotalHeight = ButtonHeight + BadgeHeight;

        // Бейдж вылезает за угол кнопки, не влияя на layout: сдвигаем его отрицательным margin'ом на
        // половину своего размера наружу к выбранному углу. Footprint контрола = размер кнопки, поэтому
        // в ряду кнопки остаются на равном расстоянии; поверх соседа бейдж выводится через ZIndex у места
        // использования.
        var left = BadgeHorizontalAlignment == HorizontalAlignment.Left ? -BadgeWidth / 2 : 0;
        var right = BadgeHorizontalAlignment == HorizontalAlignment.Right ? -BadgeWidth / 2 : 0;
        var top = BadgeVerticalAlignment == VerticalAlignment.Top ? -BadgeHeight / 2 : 0;
        var bottom = BadgeVerticalAlignment == VerticalAlignment.Bottom ? -BadgeHeight / 2 : 0;
        BadgeMargin = new Thickness(left, top, right, bottom);
    }
    
    /// <summary>
    /// Gets or sets a value indicating whether this badge is visible.
    /// </summary>
    public bool BadgeVisibility
    {
        get => GetValue(BadgeVisibilityProperty);
        set => SetValue(BadgeVisibilityProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="IBrush"/> that specifies how the badge's interior is painted.
    /// </summary>
    public IBrush? BadgeColor
    {
        get => GetValue(BadgeColorProperty);
        set => SetValue(BadgeColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="IBrush"/> that specifies how the badge's outline is painted.
    /// </summary>
    public IBrush? BadgeOutline
    {
        get => GetValue(BadgeOutlineProperty);
        set => SetValue(BadgeOutlineProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the badge outline.
    /// </summary>
    public double BadgeOutlineThickness
    {
        get => GetValue(BadgeOutlineThicknessProperty);
        set => SetValue(BadgeOutlineThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the badge
    /// </summary>
    public double BadgeWidth
    {
        get => GetValue(BadgeWidthProperty);
        set => SetValue(BadgeWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the badge
    /// </summary>
    public double BadgeHeight
    {
        get => GetValue(BadgeHeightProperty);
        set => SetValue(BadgeHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the button
    /// </summary>
    public double ButtonWidth
    {
        get => GetValue(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the button
    /// </summary>
    public double ButtonHeight
    {
        get => GetValue(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the total width of the element
    /// </summary>
    public double TotalWidth
    {
        get => GetValue(TotalWidthProperty);
        set => SetValue(TotalWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the total height of the element
    /// </summary>
    public double TotalHeight
    {
        get => GetValue(TotalHeightProperty);
        set => SetValue(TotalHeightProperty, value);
    }

    /// <summary>
    /// Gets the negative margin that makes the badge overhang the button corner without affecting layout.
    /// Computed from the badge size and alignment; not intended to be set directly.
    /// </summary>
    public Thickness BadgeMargin
    {
        get => GetValue(BadgeMarginProperty);
        private set => SetValue(BadgeMarginProperty, value);
    }

    /// <summary>
    /// Gets or sets the badge's preferred horizontal alignment in its parent.
    /// </summary>
    public HorizontalAlignment BadgeHorizontalAlignment
    {
        get => GetValue(BadgeHorizontalAlignmentProperty);
        set => SetValue(BadgeHorizontalAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the badge's preferred vertical alignment in its parent.
    /// </summary>
    public VerticalAlignment BadgeVerticalAlignment
    {
        get => GetValue(BadgeVerticalAlignmentProperty);
        set => SetValue(BadgeVerticalAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets the flyout shown when the inner button is clicked (e.g. a filter or sort panel).
    /// </summary>
    public FlyoutBase? Flyout
    {
        get => GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when the inner button is clicked.
    /// </summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the parameter passed to <see cref="Command"/> when it is invoked.
    /// </summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
}