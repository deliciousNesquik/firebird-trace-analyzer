using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Экспортер отчётов в DOCX формат
/// </summary>
public class DocxReportExporter : IReportExporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IReportProjectionService _projectionService;

    public ReportFormat Format => ReportFormat.DOCX;

    public DocxReportExporter(IReportProjectionService projectionService)
    {
        _projectionService = projectionService ?? throw new ArgumentNullException(nameof(projectionService));
    }

    public async Task ExportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Info("Exporting report to DOCX: {Path}", outputPath);

            // Сборка документа и сохранение — блокирующая работа. Уводим в фон и наблюдаем токен:
            // раньше это был fake async (всё на UI-потоке, единственная большая секция неотменяема).
            await Task.Run(() =>
            {
                using var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);

                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                ComposeHeader(body, template, metadata);

                body.AppendChild(new Paragraph(new Run(new Break())));

                foreach (var section in template.Body.Sections.OrderBy(s => s.Order))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ComposeSection(body, section, template, metadata);
                }

                if (template.Footer.Show)
                {
                    ComposeFooter(body, template);
                }

                mainPart.Document.Save();
            }, cancellationToken);

            Logger.Info("DOCX export completed: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting to DOCX");
            throw;
        }
    }

    private void ComposeHeader(Body body, ReportTemplate template, ReportMetadata metadata)
    {
        // Заголовок отчёта
        var titleParagraph = body.AppendChild(new Paragraph());
        var titleRun = titleParagraph.AppendChild(new Run());
        titleRun.AppendChild(new Text(template.Header.Title));
        
        var titleProps = titleParagraph.AppendChild(new ParagraphProperties());
        titleProps.AppendChild(new Justification { Val = JustificationValues.Center });
        
        var titleRunProps = titleRun.AppendChild(new RunProperties());
        titleRunProps.AppendChild(new Bold());
        titleRunProps.AppendChild(new FontSize { Val = "32" }); // 16pt

        // Подзаголовок
        if (!string.IsNullOrWhiteSpace(template.Header.Subtitle))
        {
            var subtitleParagraph = body.AppendChild(new Paragraph());
            var subtitleRun = subtitleParagraph.AppendChild(new Run());
            subtitleRun.AppendChild(new Text(template.Header.Subtitle));
            
            var subtitleProps = subtitleParagraph.AppendChild(new ParagraphProperties());
            subtitleProps.AppendChild(new Justification { Val = JustificationValues.Center });
            
            var subtitleRunProps = subtitleRun.AppendChild(new RunProperties());
            subtitleRunProps.AppendChild(new Italic());
            subtitleRunProps.AppendChild(new FontSize { Val = "24" }); // 12pt
        }

        // Дата генерации
        if (template.Header.ShowGeneratedDate)
        {
            var dateParagraph = body.AppendChild(new Paragraph());
            var dateRun = dateParagraph.AppendChild(new Run());
            dateRun.AppendChild(new Text(string.Format(Loc.Tr("Report.Export.Generated"), metadata.GeneratedAt.ToString(template.Header.DateFormat))));
            
            var dateProps = dateParagraph.AppendChild(new ParagraphProperties());
            dateProps.AppendChild(new Justification { Val = JustificationValues.Right });
            
            var dateRunProps = dateRun.AppendChild(new RunProperties());
            dateRunProps.AppendChild(new FontSize { Val = "18" }); // 9pt
        }

        body.AppendChild(new Paragraph(new Run(new Break())));

        // Переменные заголовка
        foreach (var variable in template.Header.Variables.Where(v => v.IsVisible).OrderBy(v => v.DisplayOrder))
        {
            var value = GetVariableValue(variable, metadata);
            
            var varParagraph = body.AppendChild(new Paragraph());
            
            var labelRun = varParagraph.AppendChild(new Run());
            labelRun.AppendChild(new Text($"{variable.DisplayName}: "));
            var labelRunProps = labelRun.AppendChild(new RunProperties());
            labelRunProps.AppendChild(new Bold());
            
            var valueRun = varParagraph.AppendChild(new Run());
            valueRun.AppendChild(new Text(value));
        }
    }

    private void ComposeSection(Body body, ReportSection section, ReportTemplate template, ReportMetadata metadata)
    {
        body.AppendChild(new Paragraph(new Run(new Break())));

        // Заголовок секции
        if (section.ShowTitle)
        {
            var sectionTitleParagraph = body.AppendChild(new Paragraph());
            var sectionTitleRun = sectionTitleParagraph.AppendChild(new Run());
            sectionTitleRun.AppendChild(new Text(section.Title));
            
            var sectionTitleRunProps = sectionTitleRun.AppendChild(new RunProperties());
            sectionTitleRunProps.AppendChild(new Bold());
            sectionTitleRunProps.AppendChild(new FontSize { Val = "28" }); // 14pt

            if (!string.IsNullOrWhiteSpace(section.Description))
            {
                var descParagraph = body.AppendChild(new Paragraph());
                var descRun = descParagraph.AppendChild(new Run());
                descRun.AppendChild(new Text(section.Description));
                
                var descRunProps = descRun.AppendChild(new RunProperties());
                descRunProps.AppendChild(new Italic());
                descRunProps.AppendChild(new FontSize { Val = "18" }); // 9pt
            }
        }

        // Содержимое секции
        switch (section.ContentType)
        {
            case SectionContentType.Events:
                ComposeEventsTable(body, template, metadata.Events);
                break;

            case SectionContentType.Statistics:
                ComposeStatistics(body, metadata);
                break;
        }
    }

    private void ComposeEventsTable(Body body, ReportTemplate template, IReadOnlyList<EventBase> events)
    {
        var data = _projectionService.BuildTable(template, events);

        var table = body.AppendChild(new Table());

        // Свойства таблицы
        var tableProps = table.AppendChild(new TableProperties());
        tableProps.AppendChild(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
        ));
        tableProps.AppendChild(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });

        // Заголовок таблицы
        var headerRow = table.AppendChild(new TableRow());
        foreach (var column in data.Columns)
        {
            var cell = headerRow.AppendChild(new TableCell());
            var paragraph = cell.AppendChild(new Paragraph());
            var run = paragraph.AppendChild(new Run());
            run.AppendChild(new Text(column.DisplayName));

            var runProps = run.AppendChild(new RunProperties());
            runProps.AppendChild(new Bold());

            // Затенение заголовка
            var cellProps = cell.AppendChild(new TableCellProperties());
            cellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "D3D3D3" });
        }

        // Строки данных
        foreach (var rowCells in data.Rows)
        {
            var dataRow = table.AppendChild(new TableRow());

            for (var i = 0; i < data.Columns.Count; i++)
            {
                var formattedValue = FormatValue(rowCells[i], data.Columns[i].Format);

                var cell = dataRow.AppendChild(new TableCell());
                var paragraph = cell.AppendChild(new Paragraph());
                var run = paragraph.AppendChild(new Run());
                run.AppendChild(new Text(formattedValue));
            }
        }
    }

    private void ComposeStatistics(Body body, ReportMetadata metadata)
    {
        body.AppendChild(new Paragraph(new Run(new Break())));

        // Поля статистики — из общего источника (см. ReportStatisticsRows), идентичного для PDF/DOCX/XLSX.
        foreach (var (label, value) in ReportStatisticsRows.Build(metadata))
            AddStatLine(body, label, value);
    }

    private void AddStatLine(Body body, string label, string value)
    {
        var paragraph = body.AppendChild(new Paragraph());
        
        var labelRun = paragraph.AppendChild(new Run());
        labelRun.AppendChild(new Text($"{label} "));
        var labelRunProps = labelRun.AppendChild(new RunProperties());
        labelRunProps.AppendChild(new Bold());
        
        var valueRun = paragraph.AppendChild(new Run());
        valueRun.AppendChild(new Text(value));
    }

    private void ComposeFooter(Body body, ReportTemplate template)
    {
        body.AppendChild(new Paragraph(new Run(new Break())));
        body.AppendChild(new Paragraph(new Run(new Break())));

        var footerParagraph = body.AppendChild(new Paragraph());
        var footerRun = footerParagraph.AppendChild(new Run());
        footerRun.AppendChild(new Text(template.Footer.Text));
        
        var footerProps = footerParagraph.AppendChild(new ParagraphProperties());
        footerProps.AppendChild(new Justification { Val = JustificationValues.Center });
        
        var footerRunProps = footerRun.AppendChild(new RunProperties());
        footerRunProps.AppendChild(new FontSize { Val = "16" }); // 8pt
    }

    // Вспомогательные методы
    private string GetVariableValue(ReportVariable variable, ReportMetadata metadata)
        => ReportMetadataFormatter.GetVariableValue(variable, metadata);

    private string FormatValue(object? value, string? format)
        => ReportValueFormatter.Format(value, format);
}