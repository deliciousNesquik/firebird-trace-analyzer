namespace FirebirdTraceAnalyzer.Models;

/// <summary>A single labeled statistic shown in the UI: a header and its value.</summary>
/// <param name="header">The statistic's label.</param>
/// <param name="value">The statistic's value, formatted for display.</param>
public class StatisticInfoModel(string header, string value)
{
    /// <summary>The statistic's label.</summary>
    public string Header { get; set; } = header;

    /// <summary>The statistic's value, formatted for display.</summary>
    public string Value { get; set; } = value;
}