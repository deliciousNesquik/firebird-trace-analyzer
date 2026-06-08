using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Экспортер отчётов в DOCX формат
/// </summary>
public class DocxReportExporter : IReportExporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IEventPropertyAccessor _propertyAccessor;

    public DocxReportExporter(IEventPropertyAccessor propertyAccessor)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
    }

    public Task ExportAsync(
        ReportTemplate template,
        ReportMetadata metadata,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Info("Exporting report to DOCX: {Path}", outputPath);

            using var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
            
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Заголовок
            ComposeHeader(body, template, metadata);

            // Разделитель
            body.AppendChild(new Paragraph(new Run(new Break())));

            // Секции отчёта
            foreach (var section in template.Body.Sections.OrderBy(s => s.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ComposeSection(body, section, template, metadata);
            }

            // Футер
            if (template.Footer.Show)
            {
                ComposeFooter(body, template);
            }

            mainPart.Document.Save();

            Logger.Info("DOCX export completed: {Path}", outputPath);

            return Task.CompletedTask;
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
            dateRun.AppendChild(new Text($"Generated: {metadata.GeneratedAt.ToString(template.Header.DateFormat)}"));
            
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
        var fields = template.Body.VisibleFields.OrderBy(f => f.Order).ToList();

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
        foreach (var field in fields)
        {
            var cell = headerRow.AppendChild(new TableCell());
            var paragraph = cell.AppendChild(new Paragraph());
            var run = paragraph.AppendChild(new Run());
            run.AppendChild(new Text(field.DisplayName));
            
            var runProps = run.AppendChild(new RunProperties());
            runProps.AppendChild(new Bold());
            
            // Затенение заголовка
            var cellProps = cell.AppendChild(new TableCellProperties());
            cellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "D3D3D3" });
        }

        // Строки данных
        foreach (var evt in events)
        {
            var dataRow = table.AppendChild(new TableRow());
            
            foreach (var field in fields)
            {
                var value = _propertyAccessor.GetValue(evt, field.PropertyPath);
                var formattedValue = FormatValue(value, field.Format);
                
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

        AddStatLine(body, "Total Files:", metadata.Files.Count.ToString());
        AddStatLine(body, "Total Events (before filters):", metadata.TotalEventsCount.ToString("N0"));
        AddStatLine(body, "Events in Report:", metadata.Events.Count.ToString("N0"));

        if (!string.IsNullOrWhiteSpace(metadata.ActiveFilters))
        {
            AddStatLine(body, "Active Filters:", metadata.ActiveFilters);
        }

        if (!string.IsNullOrWhiteSpace(metadata.ActiveSort))
        {
            AddStatLine(body, "Active Sort:", metadata.ActiveSort);
        }
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