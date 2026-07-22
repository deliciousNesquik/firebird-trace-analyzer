using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FirebirdTraceParser.Models.ValueObjects;
using NLog;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class ErrorEventCard : EventCardBase
{
    private Button? _copyButton;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_copyButton != null)
            _copyButton.Click -= CopyButtonOnClick;

        _copyButton = e.NameScope.Find<Button>("PART_CopyErrorButton");

        if (_copyButton != null)
            _copyButton.Click += CopyButtonOnClick;
    }

    private async void CopyButtonOnClick(object? sender, RoutedEventArgs e)
    {
        // async void + буфер обмена: SetTextAsync может бросить (особенно на X11/Wayland).
        // Без catch исключение из продолжения на UI-потоке роняет приложение.
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel?.Clipboard == null)
                return;

            var sb = new StringBuilder();

            sb.AppendLine($"Error at: {Component}");
            sb.AppendLine();

            if (Errors != null)
            {
                foreach (var error in Errors)
                    sb.AppendLine($"{error.ErrorCode}: {error.Message}");
            }

            await topLevel.Clipboard.SetTextAsync(sb.ToString());
        }
        catch (Exception ex)
        {
            LogManager.GetCurrentClassLogger().Warn(ex, "Failed to copy error details to clipboard");
        }
    }

    public static readonly StyledProperty<long> AttachmentIdProperty =
        AvaloniaProperty.Register<ErrorEventCard, long>(
            nameof(AttachmentId),
            0);

    public static readonly StyledProperty<string> ComponentProperty =
        AvaloniaProperty.Register<ErrorEventCard, string>(
            nameof(Component),
            "<not set>");

    public static readonly StyledProperty<IReadOnlyList<ErrorLines>> ErrorsProperty =
        AvaloniaProperty.Register<ErrorEventCard, IReadOnlyList<ErrorLines>>(
            nameof(Errors),
            Array.Empty<ErrorLines>());

    public long AttachmentId
    {
        get => GetValue(AttachmentIdProperty);
        set => SetValue(AttachmentIdProperty, value);
    }

    public string Component
    {
        get => GetValue(ComponentProperty);
        set => SetValue(ComponentProperty, value);
    }

    public IReadOnlyList<ErrorLines> Errors
    {
        get => GetValue(ErrorsProperty);
        set => SetValue(ErrorsProperty, value);
    }
}