using ClosedXML.Excel;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Экспортер отчётов в XLSX формат
/// </summary>
public class XlsxReportExporter : IReportExporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IEventPropertyAccessor _propertyAccessor;

    public XlsxReportExporter(IEventPropertyAccessor propertyAccessor)
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
            Logger.Info("Exporting report to XLSX: {Path}", outputPath);

            using var workbook = new XLWorkbook();
            
            // Создаём лист с данными
            var worksheet = workbook.Worksheets.Add("Report");

            var currentRow = 1;

            // Заголовок
            currentRow = ComposeHeader(worksheet, currentRow, template, metadata);
            currentRow += 2; // Пропускаем 2 строки

            // Секции отчёта
            foreach (var section in template.Body.Sections.OrderBy(s => s.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentRow = ComposeSection(worksheet, currentRow, section, template, metadata);
                currentRow += 2; // Разделитель между секциями
            }

            // Футер
            if (template.Footer.Show)
            {
                ComposeFooter(worksheet, currentRow, template);
            }

            // Автоподбор ширины колонок
            worksheet.Columns().AdjustToContents();

            workbook.SaveAs(outputPath);

            Logger.Info("XLSX export completed: {Path}", outputPath);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting to XLSX");
            throw;
        }
    }

    private int ComposeHeader(IXLWorksheet worksheet, int startRow, ReportTemplate template, ReportMetadata metadata)
    {
        var row = startRow;

        // Заголовок отчёта
        worksheet.Cell(row, 1).Value = template.Header.Title;
        worksheet.Cell(row, 1).Style
            .Font.SetBold()
            .Font.SetFontSize(16)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        
        worksheet.Range(row, 1, row, 10).Merge();
        row++;

        // Подзаголовок
        if (!string.IsNullOrWhiteSpace(template.Header.Subtitle))
        {
            worksheet.Cell(row, 1).Value = template.Header.Subtitle;
            worksheet.Cell(row, 1).Style
                .Font.SetItalic()
                .Font.SetFontSize(12)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            
            worksheet.Range(row, 1, row, 10).Merge();
            row++;
        }

        // Дата генерации
        if (template.Header.ShowGeneratedDate)
        {
            worksheet.Cell(row, 1).Value = $"Generated: {metadata.GeneratedAt.ToString(template.Header.DateFormat)}";
            worksheet.Cell(row, 1).Style
                .Font.SetFontSize(9)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            
            worksheet.Range(row, 1, row, 10).Merge();
            row++;
        }

        row++; // Пустая строка

        // Переменные заголовка
        foreach (var variable in template.Header.Variables.Where(v => v.IsVisible).OrderBy(v => v.DisplayOrder))
        {
            var value = GetVariableValue(variable, metadata);
            
            worksheet.Cell(row, 1).Value = $"{variable.DisplayName}:";
            worksheet.Cell(row, 1).Style.Font.SetBold();
            
            worksheet.Cell(row, 2).Value = value;
            
            row++;
        }

        return row;
    }

    private int ComposeSection(IXLWorksheet worksheet, int startRow, ReportSection section, ReportTemplate template, ReportMetadata metadata)
    {
        var row = startRow;

        // Заголовок секции
        if (section.ShowTitle)
        {
            worksheet.Cell(row, 1).Value = section.Title;
            worksheet.Cell(row, 1).Style
                .Font.SetBold()
                .Font.SetFontSize(14);
            
            worksheet.Range(row, 1, row, 10).Merge();
            row++;

            if (!string.IsNullOrWhiteSpace(section.Description))
            {
                worksheet.Cell(row, 1).Value = section.Description;
                worksheet.Cell(row, 1).Style
                    .Font.SetItalic()
                    .Font.SetFontSize(9);
                
                worksheet.Range(row, 1, row, 10).Merge();
                row++;
            }

            row++; // Пустая строка
        }

        // Содержимое секции
        switch (section.ContentType)
        {
            case SectionContentType.Events:
                row = ComposeEventsTable(worksheet, row, template, metadata.Events);
                break;

            case SectionContentType.Statistics:
                row = ComposeStatistics(worksheet, row, metadata);
                break;
        }

        return row;
    }

    private int ComposeEventsTable(IXLWorksheet worksheet, int startRow, ReportTemplate template, IReadOnlyList<EventBase> events)
    {
        var fields = template.Body.VisibleFields.OrderBy(f => f.Order).ToList();
        var row = startRow;

        // Заголовки столбцов
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var cell = worksheet.Cell(row, i + 1);
            
            cell.Value = field.DisplayName;
            cell.Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }

        row++;

        // Данные
        foreach (var evt in events)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var value = _propertyAccessor.GetValue(evt, field.PropertyPath);
                var cell = worksheet.Cell(row, i + 1);

                // Устанавливаем значение
                if (value != null)
                {
                    if (value is DateTime dateTime)
                    {
                        cell.Value = dateTime;
                        if (!string.IsNullOrWhiteSpace(field.Format))
                        {
                            cell.Style.DateFormat.Format = field.Format;
                        }
                    }
                    else if (value is int || value is long || value is decimal || value is double || value is float)
                    {
                        cell.Value = Convert.ToDouble(value);
                        if (!string.IsNullOrWhiteSpace(field.Format))
                        {
                            cell.Style.NumberFormat.Format = field.Format;
                        }
                    }
                    else
                    {
                        cell.Value = FormatValue(value, field.Format);
                    }
                }

                // Выравнивание
                cell.Style.Alignment.Horizontal = field.Alignment switch
                {
                    TextAlignment.Left => XLAlignmentHorizontalValues.Left,
                    TextAlignment.Center => XLAlignmentHorizontalValues.Center,
                    TextAlignment.Right => XLAlignmentHorizontalValues.Right,
                    _ => XLAlignmentHorizontalValues.Left
                };

                cell.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            }

            row++;
        }

        return row;
    }

    private int ComposeStatistics(IXLWorksheet worksheet, int startRow, ReportMetadata metadata)
    {
        var row = startRow;

        AddStatRow(worksheet, ref row, "Total Files:", metadata.Files.Count.ToString());
        AddStatRow(worksheet, ref row, "Total Events (before filters):", metadata.TotalEventsCount.ToString("N0"));
        AddStatRow(worksheet, ref row, "Events in Report:", metadata.Events.Count.ToString("N0"));

        if (!string.IsNullOrWhiteSpace(metadata.ActiveFilters))
        {
            AddStatRow(worksheet, ref row, "Active Filters:", metadata.ActiveFilters);
        }

        if (!string.IsNullOrWhiteSpace(metadata.ActiveSort))
        {
            AddStatRow(worksheet, ref row, "Active Sort:", metadata.ActiveSort);
        }

        return row;
    }

    private void AddStatRow(IXLWorksheet worksheet, ref int row, string label, string value)
    {
        worksheet.Cell(row, 1).Value = label;
        worksheet.Cell(row, 1).Style.Font.SetBold();
        
        worksheet.Cell(row, 2).Value = value;
        
        row++;
    }

    private void ComposeFooter(IXLWorksheet worksheet, int startRow, ReportTemplate template)
    {
        worksheet.Cell(startRow, 1).Value = template.Footer.Text;
        worksheet.Cell(startRow, 1).Style
            .Font.SetFontSize(8)
            .Font.SetItalic()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        
        worksheet.Range(startRow, 1, startRow, 10).Merge();
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