namespace FirebirdTraceParser.Parsing.Rules;

/// <summary>
/// Ключи правил в rules.json. Централизованы, чтобы опечатка ловилась компилятором,
/// а не превращалась в KeyNotFoundException на горячем пути разбора.
/// </summary>
internal static class RuleKeys
{
    public const string BlockHeader = "block_header";
    public const string Session = "session";
    public const string Attachment = "attachment";
    public const string Process = "process";
    public const string Statement = "statement";
    public const string Transaction = "transaction";
    public const string Restarted = "restarted";
    public const string Parameters = "parameters";
    public const string ErrorLine = "error_line";
    public const string Performance = "performance";
    public const string Fetched = "fetched";
    public const string Procedure = "procedure";
    public const string Trigger = "trigger";
    public const string PerformanceTableHeader = "performance_table_header";
}
