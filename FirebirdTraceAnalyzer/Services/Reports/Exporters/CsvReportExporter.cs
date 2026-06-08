using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.EventProperties;
using FirebirdTraceParser.Models.Events;
using NLog;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Экспортер отчётов в CSV формат
/// </summary>
public class CsvReportExporter : IReportExporter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IEventPropertyAccessor _propertyAccessor;

    public CsvReportExporter(IEventPropertyAccessor propertyAccessor)
    {
        _propertyAccessor = propertyAccessor ?? throw new ArgumentNullException(nameof(propertyAccessor));
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

            // Записываем заголовки столбцов
            await WriteHeadersAsync(csv, template, cancellationToken);

            // Записываем события
            await WriteEventsAsync(csv, template, metadata.Events, cancellationToken);

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

        csv.WriteField($"# Generated: {metadata.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        await csv.NextRecordAsync();

        csv.WriteField($"# Application: Flytic v{metadata.ApplicationVersion}");
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
        ReportTemplate template,
        CancellationToken cancellationToken)
    {
        var fields = template.Body.VisibleFields.OrderBy(f => f.Order).ToList();

        foreach (var field in fields)
        {
            csv.WriteField(field.DisplayName);
        }

        await csv.NextRecordAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task WriteEventsAsync(
        CsvWriter csv,
        ReportTemplate template,
        IReadOnlyList<EventBase> events,
        CancellationToken cancellationToken)
    {
        var fields = template.Body.VisibleFields.OrderBy(f => f.Order).ToList();

        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var field in fields)
            {
                var value = _propertyAccessor.GetValue(evt, field.PropertyPath);
                var formattedValue = FormatValue(value, field.Format);
                csv.WriteField(formattedValue);
            }

            await csv.NextRecordAsync();
        }
    }

    private async Task WriteSummaryAsync(
        CsvWriter csv,
        ReportMetadata metadata,
        CancellationToken cancellationToken)
    {
        csv.WriteField("# Summary Statistics");
        await csv.NextRecordAsync();

        csv.WriteField("# Total Files");
        csv.WriteField(metadata.Files.Count);
        await csv.NextRecordAsync();

        csv.WriteField("# Total Events (before filters)");
        csv.WriteField(metadata.TotalEventsCount);
        await csv.NextRecordAsync();

        csv.WriteField("# Events in Report");
        csv.WriteField(metadata.Events.Count);
        await csv.NextRecordAsync();

        if (!string.IsNullOrWhiteSpace(metadata.ActiveFilters))
        {
            csv.WriteField("# Active Filters");
            csv.WriteField(metadata.ActiveFilters);
            await csv.NextRecordAsync();
        }

        if (!string.IsNullOrWhiteSpace(metadata.ActiveSort))
        {
            csv.WriteField("# Active Sort");
            csv.WriteField(metadata.ActiveSort);
            await csv.NextRecordAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private string GetVariableValue(ReportVariable variable, ReportMetadata metadata)
    {
        return variable.Type switch
        {
            Enums.Reports.ReportVariableType.FileNames => string.Join(", ", metadata.Files.Select(f => f.FileName)),
            Enums.Reports.ReportVariableType.FilePaths => string.Join(", ", metadata.Files.Select(f => f.FilePath)),
            Enums.Reports.ReportVariableType.FileCount => metadata.Files.Count.ToString(),
            Enums.Reports.ReportVariableType.FileSizeTotal => FormatFileSize(metadata.Files.Sum(f => f.FileSize)),
            
            Enums.Reports.ReportVariableType.TotalEventsCount => metadata.TotalEventsCount.ToString("N0"),
            Enums.Reports.ReportVariableType.FilteredEventsCount => metadata.Events.Count.ToString("N0"),
            Enums.Reports.ReportVariableType.VisibleEventsCount => metadata.Events.Count.ToString("N0"),
            
            Enums.Reports.ReportVariableType.TraceStartTime => metadata.Files.Count > 0 
                ? metadata.Files.Min(f => f.StartTrace).ToString("yyyy-MM-dd HH:mm:ss") 
                : "N/A",
            Enums.Reports.ReportVariableType.TraceEndTime => metadata.Files.Count > 0 
                ? metadata.Files.Max(f => f.EndTrace).ToString("yyyy-MM-dd HH:mm:ss") 
                : "N/A",
            Enums.Reports.ReportVariableType.TraceDuration => GetTraceDuration(metadata),
            
            Enums.Reports.ReportVariableType.ActiveFilters => metadata.ActiveFilters ?? "None",
            Enums.Reports.ReportVariableType.ActiveSort => metadata.ActiveSort ?? "None",
            
            Enums.Reports.ReportVariableType.GeneratedDate => metadata.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            Enums.Reports.ReportVariableType.GeneratedBy => Environment.UserName,
            Enums.Reports.ReportVariableType.ApplicationVersion => metadata.ApplicationVersion,
            
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
        if (value == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(format))
        {
            if (value is IFormattable formattable)
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture);
            }
        }

        return value.ToString() ?? string.Empty;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        var size = (double)bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}