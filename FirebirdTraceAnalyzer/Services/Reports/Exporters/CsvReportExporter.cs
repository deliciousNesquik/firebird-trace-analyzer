using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FirebirdTraceAnalyzer.Interfaces.Reports;
using FirebirdTraceAnalyzer.Interfaces.Reports.Exporters;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Экспортер отчётов в CSV формат
/// </summary>
public class CsvReportExporter : IReportExporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IReportProjectionService _projectionService;

    public CsvReportExporter(IReportProjectionService projectionService)
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
            Logger.Info("Exporting report to CSV: {Path}", outputPath);

            await using var writer = new StreamWriter(outputPath);
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = true
            });

            // Записываем метаданные в начало файла
            await WriteMetadataAsync(csv, template, metadata, cancellationToken);

            // Пустая строка для разделения
            await csv.NextRecordAsync();

            // Строим таблицу (колонки + строки) и пишем заголовки и данные из неё
            var table = _projectionService.BuildTable(template, metadata.Events);

            // Записываем заголовки столбцов
            await WriteHeadersAsync(csv, table, cancellationToken);

            // Записываем события
            await WriteEventsAsync(csv, table, cancellationToken);

            // Записываем статистику (если включена)
            if (template.Body.ShowSummary)
            {
                await csv.NextRecordAsync();
                await WriteSummaryAsync(csv, metadata, cancellationToken);
            }

            Logger.Info("CSV export completed: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error exporting to CSV");
            throw;
        }
    }

    private async Task WriteMetadataAsync(
        CsvWriter csv,
        ReportTemplate template,
        ReportMetadata metadata,
        CancellationToken cancellationToken)
    {
        // Записываем метаданные как комментарии
        csv.WriteField($"# {template.Header.Title}");
        await csv.NextRecordAsync();

        if (!string.IsNullOrWhiteSpace(template.Header.Subtitle))
        {
            csv.WriteField($"# {template.Header.Subtitle}");
            await csv.NextRecordAsync();
        }

        csv.WriteField($"# {string.Format(Loc.Tr("Report.Export.Generated"), metadata.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))}");
        await csv.NextRecordAsync();

        csv.WriteField($"# {string.Format(Loc.Tr("Report.Export.Application"), metadata.ApplicationVersion)}");
        await csv.NextRecordAsync();

        // Записываем переменные заголовка
        foreach (var variable in template.Header.Variables.Where(v => v.IsVisible).OrderBy(v => v.DisplayOrder))
        {
            var value = GetVariableValue(variable, metadata);
            csv.WriteField($"# {variable.DisplayName}: {value}");
            await csv.NextRecordAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task WriteHeadersAsync(
        CsvWriter csv,
        ReportTable table,
        CancellationToken cancellationToken)
    {
        foreach (var column in table.Columns)
        {
            csv.WriteField(column.DisplayName);
        }

        await csv.NextRecordAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task WriteEventsAsync(
        CsvWriter csv,
        ReportTable table,
        CancellationToken cancellationToken)
    {
        foreach (var rowCells in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var i = 0; i < table.Columns.Count; i++)
            {
                csv.WriteField(FormatValue(rowCells[i], table.Columns[i].Format));
            }

            await csv.NextRecordAsync();
        }
    }

    private async Task WriteSummaryAsync(
        CsvWriter csv,
        ReportMetadata metadata,
        CancellationToken cancellationToken)
    {
        csv.WriteField($"# {Loc.Tr("Report.Export.SummaryStatistics")}");
        await csv.NextRecordAsync();

        csv.WriteField($"# {Loc.Tr("Report.Export.CsvTotalFiles")}");
        csv.WriteField(metadata.Files.Count);
        await csv.NextRecordAsync();

        csv.WriteField($"# {Loc.Tr("Report.Export.CsvTotalEventsBeforeFilters")}");
        csv.WriteField(metadata.TotalEventsCount);
        await csv.NextRecordAsync();

        csv.WriteField($"# {Loc.Tr("Report.Export.CsvEventsInReport")}");
        csv.WriteField(metadata.Events.Count);
        await csv.NextRecordAsync();

        if (!string.IsNullOrWhiteSpace(metadata.ActiveFilters))
        {
            csv.WriteField($"# {Loc.Tr("Report.Export.CsvActiveFilters")}");
            csv.WriteField(metadata.ActiveFilters);
            await csv.NextRecordAsync();
        }

        if (!string.IsNullOrWhiteSpace(metadata.ActiveSort))
        {
            csv.WriteField($"# {Loc.Tr("Report.Export.CsvActiveSort")}");
            csv.WriteField(metadata.ActiveSort);
            await csv.NextRecordAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private string GetVariableValue(ReportVariable variable, ReportMetadata metadata)
        => ReportMetadataFormatter.GetVariableValue(variable, metadata);

    private string FormatValue(object? value, string? format)
        => ReportValueFormatter.Format(value, format);
}