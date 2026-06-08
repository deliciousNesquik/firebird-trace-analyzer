using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceParser.Models.Events;
using NLog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Экспортер отчётов в PDF формат с использованием QuestPDF
/// </summary>
public class PdfReportExporter : IReportExporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IEventPropertyAccessor _propertyAccessor;

    public PdfReportExporter(IEventPropertyAccessor propertyAccessor)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
    }

    static PdfReportExporter()
    {
        // Настройка QuestPDF лицензии (Community License для некоммерческого использования)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task ExportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Info("Exporting report to PDF: {Path}", outputPath);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                    // Заголовок
                    page.Header().Element(c => ComposeHeader(c, template, metadata));

                    // Содержимое
                    page.Content().Element(c => ComposeContent(c, template, metadata));

                    // Футер
                    page.Footer().Element(c => ComposeFooter(c, template));
                });
            });

            document.GeneratePdf(outputPath);

            Logger.Info("PDF export completed: {Path}", outputPath);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting to PDF");
            throw;
        }
    }

    private void ComposeHeader(IContainer container, ReportTemplate template, ReportMetadata metadata)
    {
        container.Column(column =>
        {
            column.Spacing(5);

            // Название отчёта
            column.Item().AlignCenter().Text(template.Header.Title)
                .FontSize(18)
                .Bold()
                .FontColor(Colors.Blue.Darken2);

            // Подзаголовок
            if (!string.IsNullOrWhiteSpace(template.Header.Subtitle))
            {
                column.Item().AlignCenter().Text(template.Header.Subtitle)
                    .FontSize(12)
                    .Italic()
                    .FontColor(Colors.Grey.Darken1);
            }

            // Дата генерации
            if (template.Header.ShowGeneratedDate)
            {
                column.Item().AlignRight().Text($"Generated: {metadata.GeneratedAt.ToString(template.Header.DateFormat)}")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            }

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            // Переменные заголовка
            foreach (var variable in template.Header.Variables.Where(v => v.IsVisible).OrderBy(v => v.DisplayOrder))
            {
                var value = GetVariableValue(variable, metadata);
                
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(variable.DisplayName)
                        .FontSize(9)
                        .Bold();
                    
                    row.RelativeItem().Text(value)
                        .FontSize(9);
                });
            }
        });
    }

    private void ComposeContent(IContainer container, ReportTemplate template, ReportMetadata metadata)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            // Секции отчёта
            foreach (var section in template.Body.Sections.OrderBy(s => s.Order))
            {
                column.Item().Element(c => ComposeSection(c, section, template, metadata));
            }
        });
    }

    private void ComposeSection(IContainer container, ReportSection section, ReportTemplate template, ReportMetadata metadata)
    {
        container.Column(column =>
        {
            column.Spacing(5);

            // Заголовок секции
            if (section.ShowTitle)
            {
                column.Item().Text(section.Title)
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                if (!string.IsNullOrWhiteSpace(section.Description))
                {
                    column.Item().Text(section.Description)
                        .FontSize(9)
                        .Italic()
                        .FontColor(Colors.Grey.Darken1);
                }
            }

            // Содержимое секции
            switch (section.ContentType)
            {
                case SectionContentType.Events:
                    column.Item().Element(c => ComposeEventsTable(c, template, metadata.Events));
                    break;

                case SectionContentType.Statistics:
                    column.Item().Element(c => ComposeStatistics(c, metadata));
                    break;
            }
        });
    }

    private void ComposeEventsTable(IContainer container, ReportTemplate template, IReadOnlyList<EventBase> events)
    {
        var fields = template.Body.VisibleFields.OrderBy(f => f.Order).ToList();

        container.Table(table =>
        {
            // Определяем колонки
            table.ColumnsDefinition(columns =>
            {
                foreach (var field in fields)
                {
                    if (field.WidthPercent.HasValue)
                    {
                        columns.RelativeColumn((float)field.WidthPercent.Value);
                    }
                    else
                    {
                        columns.RelativeColumn();
                    }
                }
            });

            // Заголовок таблицы
            table.Header(header =>
            {
                foreach (var field in fields)
                {
                    header.Cell().Element(CellStyle).Text(field.DisplayName)
                        .FontSize(9)
                        .Bold();
                }

                static IContainer CellStyle(IContainer c) => c
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten1)
                    .Background(Colors.Grey.Lighten3)
                    .Padding(5);
            });

            // Строки данных
            foreach (var evt in events)
            {
                foreach (var field in fields)
                {
                    var value = _propertyAccessor.GetValue(evt, field.PropertyPath);
                    var formattedValue = FormatValue(value, field.Format);

                    table.Cell().Element(CellStyle).Text(formattedValue)
                        .FontSize(8);
                }

                static IContainer CellStyle(IContainer c) => c
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Padding(5);
            }
        });
    }

    private void ComposeStatistics(IContainer container, ReportMetadata metadata)
    {
        container.Column(column =>
        {
            column.Spacing(3);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Total Files:").Bold();
                row.RelativeItem().Text(metadata.Files.Count.ToString());
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Total Events (before filters):").Bold();
                row.RelativeItem().Text(metadata.TotalEventsCount.ToString("N0"));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Events in Report:").Bold();
                row.RelativeItem().Text(metadata.Events.Count.ToString("N0"));
            });

            if (!string.IsNullOrWhiteSpace(metadata.ActiveFilters))
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Active Filters:").Bold();
                    row.RelativeItem().Text(metadata.ActiveFilters);
                });
            }

            if (!string.IsNullOrWhiteSpace(metadata.ActiveSort))
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Active Sort:").Bold();
                    row.RelativeItem().Text(metadata.ActiveSort);
                });
            }
        });
    }

    private void ComposeFooter(IContainer container, ReportTemplate template)
    {
        if (!template.Footer.Show)
            return;

        container.Column(column =>
        {
            column.Spacing(5);

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            column.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(template.Footer.Text)
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);

                if (template.Footer.ShowPageNumbers)
                {
                    row.RelativeItem()
                        .AlignRight()
                        .Text(text =>
                        {
                            // Задаем базовый стиль для всего текстового блока внутри
                            text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                }
            });
        });
    }

    // Вспомогательные методы (аналогичные CSV экспортеру)
    private string GetVariableValue(ReportVariable variable, ReportMetadata metadata)
    {
        return variable.Type switch
        {
            ReportVariableType.FileNames => string.Join(", ", metadata.Files.Select(f => f.FileName)),
            ReportVariableType.FileCount => metadata.Files.Count.ToString(),
            ReportVariableType.TotalEventsCount => metadata.TotalEventsCount.ToString("N0"),
            ReportVariableType.FilteredEventsCount => metadata.Events.Count.ToString("N0"),
            ReportVariableType.TraceDuration => GetTraceDuration(metadata),
            ReportVariableType.ActiveFilters => metadata.ActiveFilters ?? "None",
            ReportVariableType.ActiveSort => metadata.ActiveSort ?? "None",
            ReportVariableType.GeneratedDate => metadata.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ReportVariableType.GeneratedBy => Environment.UserName,
            ReportVariableType.ApplicationVersion => metadata.ApplicationVersion,
            _ => "N/A"
        };
    }

    private string GetTraceDuration(ReportMetadata metadata)
    {
        if (metadata.Files.Count == 0)
            return "N/A";

        var start = metadata.Files.Min(f => f.StartTrace);
        var end = metadata.Files.Max(f => f.EndTrace);
        var duration = end - start;

        return $"{duration.TotalHours:F2} hours";
    }

    private string FormatValue(object? value, string? format)
    {
        if (value == null) return string.Empty;

        if (!string.IsNullOrWhiteSpace(format) && value is IFormattable formattable)
        {
            return formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }
}