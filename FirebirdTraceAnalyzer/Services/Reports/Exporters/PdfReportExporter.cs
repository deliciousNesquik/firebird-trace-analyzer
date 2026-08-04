using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
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
    private readonly IReportProjectionService _projectionService;

    public ReportFormat Format => ReportFormat.PDF;

    public PdfReportExporter(IReportProjectionService projectionService)
    {
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
    }

    static PdfReportExporter()
    {
        // Настройка QuestPDF лицензии (Community License для некоммерческого использования)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task ExportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Info("Exporting report to PDF: {Path}", outputPath);

            // Сборка таблицы событий и рендер PDF — блокирующая CPU-работа. Уводим в фон и наблюдаем
            // токен: раньше это был fake async (GeneratePdf на UI-потоке, отмена игнорировалась).
            await Task.Run(() =>
            {
                // Таблицу событий считаем ОДИН раз на весь экспорт (группировка/сортировка/рефлексия по
                // всем событиям — дорого): Lazy потокобезопасен и вычислится только при наличии Events-секции.
                var eventsTable = new Lazy<ReportTable>(() => _projectionService.BuildTable(template, metadata.Events));

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                        page.Header().Element(c => ComposeHeader(c, template, metadata));
                        page.Content().Element(c => ComposeContent(c, template, metadata, eventsTable));
                        page.Footer().Element(c => ComposeFooter(c, template));
                    });
                });

                cancellationToken.ThrowIfCancellationRequested();
                document.GeneratePdf(outputPath);
            }, cancellationToken);

            Logger.Info("PDF export completed: {Path}", outputPath);
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
                column.Item().AlignRight().Text(string.Format(Loc.Tr("Report.Export.Generated"), metadata.GeneratedAt.ToString(template.Header.DateFormat)))
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken1);
            }

            column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    /// <summary>
    /// Блок переменных заголовка (метаданные отчёта: список файлов, счётчики и т.п.).
    /// Рендерится в теле документа (page.Content), а НЕ в повторяющемся page.Header —
    /// иначе большой список (напр. имена 100+ файлов) перерастает высоту страницы,
    /// а колонтитул не умеет разбиваться на страницы, и QuestPDF падает с
    /// DocumentLayoutException ("conflicting size constraints").
    /// </summary>
    private void ComposeReportInfo(IContainer container, ReportTemplate template, ReportMetadata metadata)
    {
        container.Column(column =>
        {
            column.Spacing(3);

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

    private void ComposeContent(IContainer container, ReportTemplate template, ReportMetadata metadata,
        Lazy<ReportTable> eventsTable)
    {
        container.Column(column =>
        {
            column.Spacing(10);

            // Метаданные отчёта (переменные заголовка) — в начале тела, чтобы длинный
            // список файлов мог разбиваться на страницы.
            if (template.Header.Variables.Any(v => v.IsVisible))
            {
                column.Item().Element(c => ComposeReportInfo(c, template, metadata));
            }

            // Секции отчёта
            foreach (var section in template.Body.Sections.OrderBy(s => s.Order))
            {
                column.Item().Element(c => ComposeSection(c, section, template, metadata, eventsTable));
            }
        });
    }

    private void ComposeSection(IContainer container, ReportSection section, ReportTemplate template, ReportMetadata metadata,
        Lazy<ReportTable> eventsTable)
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
                    column.Item().Element(c => ComposeEventsTable(c, eventsTable.Value));
                    break;

                case SectionContentType.Statistics:
                    column.Item().Element(c => ComposeStatistics(c, metadata));
                    break;
            }
        });
    }

    private void ComposeEventsTable(IContainer container, ReportTable data)
    {
        container.Table(table =>
        {
            // Определяем колонки
            table.ColumnsDefinition(columns =>
            {
                foreach (var column in data.Columns)
                {
                    if (column.WidthPercent.HasValue)
                    {
                        columns.RelativeColumn((float)column.WidthPercent.Value);
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
                foreach (var column in data.Columns)
                {
                    header.Cell().Element(CellStyle).Text(column.DisplayName)
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
            foreach (var rowCells in data.Rows)
            {
                for (var i = 0; i < data.Columns.Count; i++)
                {
                    var formattedValue = FormatValue(rowCells[i], data.Columns[i].Format);

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

            // Поля статистики — из общего источника (см. ReportStatisticsRows), идентичного для PDF/DOCX/XLSX.
            foreach (var (label, value) in ReportStatisticsRows.Build(metadata))
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(label).Bold();
                    row.RelativeItem().Text(value);
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
                            text.Span(Loc.Tr("Report.Export.Page"));
                            text.CurrentPageNumber();
                            text.Span(Loc.Tr("Report.Export.PageOf"));
                            text.TotalPages();
                        });
                }
            });
        });
    }

    // Вспомогательные методы (аналогичные CSV экспортеру)
    private string GetVariableValue(ReportVariable variable, ReportMetadata metadata)
        => ReportMetadataFormatter.GetVariableValue(variable, metadata);

    private string FormatValue(object? value, string? format)
        => ReportValueFormatter.Format(value, format);
}