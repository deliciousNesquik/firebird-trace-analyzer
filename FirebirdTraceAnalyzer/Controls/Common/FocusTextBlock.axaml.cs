using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace FirebirdTraceAnalyzer.Controls;

public class FocusTextBlock : TemplatedControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<FocusTextBlock, string>(nameof(Header), "");
    
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<FocusTextBlock, string>(nameof(Value), "");
 
    
    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }
    
    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}