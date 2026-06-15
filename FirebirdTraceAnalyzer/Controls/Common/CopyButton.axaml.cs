using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace FirebirdTraceAnalyzer.Controls;

[PseudoClasses(":copied")]
public class CopyButton : TemplatedControl
{
    private Button? _innerButton;

    // Регистрация свойства Text для внешних привязок
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        if (_innerButton != null)
            _innerButton.Click -= OnInnerButtonClick;

        _innerButton = e.NameScope.Find<Button>("PART_InnerButton");
        
        if (_innerButton != null)
            _innerButton.Click += OnInnerButtonClick;
    }

    private async void OnInnerButtonClick(object? sender, RoutedEventArgs e)
    {
        // Выполняем копирование, если текст задан
        if (!string.IsNullOrEmpty(Text))
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(Text);
            }
        }

        // Запуск анимации трансформации
        PseudoClasses.Set(":copied", true);
        await Task.Delay(2000);
        PseudoClasses.Set(":copied", false);
    }
}