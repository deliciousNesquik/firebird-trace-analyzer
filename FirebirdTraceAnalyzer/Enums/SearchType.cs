namespace FirebirdTraceAnalyzer.Enums;

/// <summary>
/// Defines the types of search operations that can be performed.
/// </summary>
public enum SearchType
{
    /// <summary>
    /// Represents a classic search type, which typically involves simple string matching.
    /// </summary>
    Classic,
    
    /// <summary>
    /// Represents a regex search type, which allows for more complex pattern matching using regular expressions.
    /// </summary>
    Regex
}