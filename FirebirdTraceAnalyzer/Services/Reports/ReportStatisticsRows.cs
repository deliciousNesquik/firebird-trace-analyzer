using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;

namespace FirebirdTraceAnalyzer.Services.Reports;

/// <summary>
/// Единый источник строк блока статистики отчёта (метка + значение) для форматов, у которых он
/// идентичен: PDF, DOCX, XLSX. Раньше этот список из 3 обязательных + 2 условных полей (фильтры/
/// сортировка) с одинаковыми loc-ключами и форматированием был продублирован в каждом экспортёре.
///
/// CSV НЕ использует этот источник намеренно: у него собственные loc-ключи (Report.Export.Csv*) и
/// сырые числа без формата "N0" — его блок статистики отличается по построению.
/// </summary>
public static class ReportStatisticsRows
{
    public static IReadOnlyList<(string Label, string Value)> Build(ReportMetadata metadata)
    {
        var rows = new List<(string Label, string Value)>
        {
            (Loc.Tr("Report.Export.TotalFiles"), metadata.Files.Count.ToString()),
            (Loc.Tr("Report.Export.TotalEventsBeforeFilters"), metadata.TotalEventsCount.ToString("N0")),
            (Loc.Tr("Report.Export.EventsInReport"), metadata.Events.Count.ToString("N0"))
        };

        if (!string.IsNullOrWhiteSpace(metadata.ActiveFilters))
            rows.Add((Loc.Tr("Report.Export.ActiveFilters"), metadata.ActiveFilters));

        if (!string.IsNullOrWhiteSpace(metadata.ActiveSort))
            rows.Add((Loc.Tr("Report.Export.ActiveSort"), metadata.ActiveSort!));

        return rows;
    }
}
