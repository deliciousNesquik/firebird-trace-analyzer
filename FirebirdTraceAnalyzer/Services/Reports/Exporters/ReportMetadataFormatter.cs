using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.Enums.Reports;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;

namespace FirebirdTraceAnalyzer.Services.Reports.Exporters;

/// <summary>
/// Единое форматирование значений переменных заголовка отчёта и длительности трассировки.
/// Заменяет копии GetVariableValue/GetTraceDuration в экспортёрах и в ReportPreviewViewModel.
/// Набор поддерживаемых переменных — расширенный (пути файлов, суммарный размер, временные
/// диапазоны), которого не было в копиях экспортёров.
/// </summary>
public static class ReportMetadataFormatter
{
    /// <summary>Значение переменной заголовка отчёта. Возвращает "N/A", если данных нет.</summary>
    public static string GetVariableValue(ReportVariable variable, ReportMetadata metadata)
    {
        return variable.Type switch
        {
            ReportVariableType.FileNames => string.Join(", ", metadata.Files.Select(f => f.FileName)),
            ReportVariableType.FilePaths => string.Join(", ", metadata.Files.Select(f => f.FilePath)),
            ReportVariableType.FileCount => metadata.Files.Count.ToString(),
            ReportVariableType.FileSizeTotal => ByteSizeFormatter.FormatBytes(metadata.Files.Sum(f => f.FileSize)),

            ReportVariableType.TotalEventsCount => metadata.TotalEventsCount.ToString("N0"),
            ReportVariableType.FilteredEventsCount => metadata.Events.Count.ToString("N0"),
            ReportVariableType.VisibleEventsCount => metadata.Events.Count.ToString("N0"),

            ReportVariableType.TraceStartTime => metadata.Files.Count > 0
                ? metadata.Files.Min(f => f.StartTrace).ToString("yyyy-MM-dd HH:mm:ss")
                : Loc.Tr("Report.Export.NotAvailable"),
            ReportVariableType.TraceEndTime => metadata.Files.Count > 0
                ? metadata.Files.Max(f => f.EndTrace).ToString("yyyy-MM-dd HH:mm:ss")
                : Loc.Tr("Report.Export.NotAvailable"),
            ReportVariableType.TraceDuration => GetTraceDuration(metadata),

            ReportVariableType.ActiveFilters => metadata.ActiveFilters ?? Loc.Tr("Report.Export.None"),
            ReportVariableType.ActiveSort => metadata.ActiveSort ?? Loc.Tr("Report.Export.None"),

            ReportVariableType.GeneratedDate => metadata.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ReportVariableType.GeneratedBy => Environment.UserName,
            ReportVariableType.ApplicationVersion => metadata.ApplicationVersion,

            _ => Loc.Tr("Report.Export.NotAvailable")
        };
    }

    /// <summary>Длительность трассировки в часах ("N/A", если файлов нет).</summary>
    public static string GetTraceDuration(ReportMetadata metadata)
    {
        if (metadata.Files.Count == 0)
            return Loc.Tr("Report.Export.NotAvailable");

        var start = metadata.Files.Min(f => f.StartTrace);
        var end = metadata.Files.Max(f => f.EndTrace);
        var duration = end - start;

        return string.Format(Loc.Tr("Report.Export.DurationHours"), duration.TotalHours.ToString("F2"));
    }
}
