using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using FirebirdTraceParser.Models.Events;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

public class ErrorEventCard : TemplatedControl
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

    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<ErrorEventCard, DateTime>(
            nameof(Timestamp),
            DateTime.MinValue);

    public static readonly StyledProperty<int> TraceIdProperty =
        AvaloniaProperty.Register<ErrorEventCard, int>(
            nameof(TraceId),
            0);

    public static readonly StyledProperty<string> HexTraceIdProperty =
        AvaloniaProperty.Register<ErrorEventCard, string>(
            nameof(HexTraceId),
            "0");

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

    public DateTime Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    public int TraceId
    {
        get => GetValue(TraceIdProperty);
        set => SetValue(TraceIdProperty, value);
    }

    public string HexTraceId
    {
        get => GetValue(HexTraceIdProperty);
        set => SetValue(HexTraceIdProperty, value);
    }

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