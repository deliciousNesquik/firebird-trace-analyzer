using System.Globalization;
using ClosedXML.Excel;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Экспортер отчётов в XLSX формат
/// </summary>
public class XlsxReportExporter : IReportExporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>Сколько первых строк учитывать при автоподборе ширины колонок (см. AdjustToContents).</summary>
    private const int WidthSampleRows = 200;

    private readonly IReportProjectionService _projectionService;

    public ReportFormat Format => ReportFormat.XLSX;

    public XlsxReportExporter(IReportProjectionService projectionService)
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
            Logger.Info("Exporting report to XLSX: {Path}", outputPath);

            // Сборка книги и сохранение — блокирующая работа. Уводим в фон и наблюдаем токен:
            // раньше это был fake async (всё на UI-потоке, единственная большая секция неотменяема).
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();

                // Создаём лист с данными
                var worksheet = workbook.Worksheets.Add(Loc.Tr("Report.Export.WorksheetName"));

                var currentRow = 1;

                currentRow = ComposeHeader(worksheet, currentRow, template, metadata);
                currentRow += 2; // Пропускаем 2 строки

                // Проекцию событий считаем ОДИН раз на весь экспорт (как PDF): Lazy вычисляется только
                // при наличии Events-секции и не пересобирается для каждой такой секции.
                var eventsTable = new Lazy<ReportTable>(() => _projectionService.BuildTable(template, metadata.Events));

                foreach (var section in template.Body.Sections.OrderBy(s => s.Order))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    currentRow = ComposeSection(worksheet, currentRow, section, metadata, eventsTable);
                    currentRow += 2; // Разделитель между секциями
                }

                if (template.Footer.Show)
                {
                    ComposeFooter(worksheet, currentRow, template);
                }

                // Автоподбор ширины колонок по ВЫБОРКЕ строк (заголовок + первые N), а не по всем ячейкам:
                // AdjustToContents() без границ сканирует каждую ячейку — O(строк×колонок), что на больших
                // отчётах (миллионы строк) недопустимо. Выборки достаточно для разумной ширины.
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                worksheet.Columns().AdjustToContents(1, Math.Min(lastRow, WidthSampleRows));

                workbook.SaveAs(outputPath);
            }, cancellationToken);

            Logger.Info("XLSX export completed: {Path}", outputPath);
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
            worksheet.Cell(row, 1).Value = string.Format(Loc.Tr("Report.Export.Generated"), metadata.GeneratedAt.ToString(template.Header.DateFormat));
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

    private int ComposeSection(IXLWorksheet worksheet, int startRow, ReportSection section, ReportMetadata metadata, Lazy<ReportTable> eventsTable)
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
                row = ComposeEventsTable(worksheet, row, eventsTable.Value);
                break;

            case SectionContentType.Statistics:
                row = ComposeStatistics(worksheet, row, metadata);
                break;
        }

        return row;
    }

    private int ComposeEventsTable(IXLWorksheet worksheet, int startRow, ReportTable data)
    {
        var row = startRow;

        // Заголовки столбцов
        for (var i = 0; i < data.Columns.Count; i++)
        {
            var cell = worksheet.Cell(row, i + 1);

            cell.Value = data.Columns[i].DisplayName;
            cell.Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }

        row++;

        // Данные
        foreach (var rowCells in data.Rows)
        {
            for (var i = 0; i < data.Columns.Count; i++)
            {
                var column = data.Columns[i];
                var value = rowCells[i];
                var cell = worksheet.Cell(row, i + 1);

                // Устанавливаем значение (типизированно — чтобы Excel правильно форматировал даты/числа)
                if (value != null)
                {
                    if (value is DateTime dateTime)
                    {
                        cell.Value = dateTime;
                        if (!string.IsNullOrWhiteSpace(column.Format))
                        {
                            cell.Style.DateFormat.Format = column.Format;
                        }
                    }
                    else if (value is int || value is long || value is decimal || value is double || value is float)
                    {
                        cell.Value = Convert.ToDouble(value);
                        if (!string.IsNullOrWhiteSpace(column.Format))
                        {
                            // Excel НЕ понимает .NET-спецификаторы ("N0" он покажет как "N1234").
                            // Переводим в код формата Excel; нераспознанный формат не ставим (число как есть).
                            var excelFormat = ToExcelNumberFormat(column.Format);
                            if (excelFormat != null)
                                cell.Style.NumberFormat.Format = excelFormat;
                        }
                    }
                    else
                    {
                        cell.Value = FormatValue(value, column.Format);
                    }
                }

                // Выравнивание
                cell.Style.Alignment.Horizontal = column.Alignment switch
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

    /// <summary>
    /// Переводит .NET-строку числового формата ("N0", "F2", "P1", "D5") в код формата Excel
    /// ("#,##0", "0.00", "0.0%", "00000"). Excel не понимает .NET-спецификаторы и показал бы "N0"
    /// буквально ("N1234"). Возвращает null для нераспознанного — тогда число рисуется без формата.
    /// </summary>
    private static string? ToExcelNumberFormat(string netFormat)
    {
        var f = netFormat.Trim();
        if (f.Length == 0)
            return null;

        var specifier = char.ToUpperInvariant(f[0]);

        // Уже код Excel (плейсхолдеры), а не .NET-спецификатор — используем как есть.
        if (specifier is not ('N' or 'F' or 'D' or 'P' or 'E' or 'G' or 'C') && f.IndexOfAny(['#', '0', '%']) >= 0)
            return f;

        var digits = -1; // -1 = точность не указана явно
        if (f.Length > 1 && int.TryParse(f.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            digits = parsed;

        static string Decimals(int count) => count > 0 ? "." + new string('0', count) : string.Empty;

        return specifier switch
        {
            'N' => "#,##0" + Decimals(digits >= 0 ? digits : 2), // группировка тысяч + N знаков (по умолч. 2)
            'F' => "0" + Decimals(digits >= 0 ? digits : 2),      // фикс. точка без группировки
            'P' => "0" + Decimals(digits >= 0 ? digits : 2) + "%", // проценты
            'D' => new string('0', digits > 0 ? digits : 1),      // целое с мин. числом цифр (ведущие нули)
            _ => null
        };
    }

    private int ComposeStatistics(IXLWorksheet worksheet, int startRow, ReportMetadata metadata)
    {
        var row = startRow;

        // Поля статистики — из общего источника (см. ReportStatisticsRows), идентичного для PDF/DOCX/XLSX.
        foreach (var (label, value) in ReportStatisticsRows.Build(metadata))
            AddStatRow(worksheet, ref row, label, value);

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
        => ReportMetadataFormatter.GetVariableValue(variable, metadata);

    private string FormatValue(object? value, string? format)
        => ReportValueFormatter.Format(value, format);
}