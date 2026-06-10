namespace FirebirdTraceParser.Exceptions;

/// <summary>
/// Базовое исключение библиотеки.
/// Соответствует концепции RuleConfigError в Python.
/// </summary>
public abstract class FirebirdParseException : Exception
{
    protected FirebirdParseException(string message) : base(message) { }
    protected FirebirdParseException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Ошибка валидации правил парсинга.
/// Аналог Python RuleConfigError.
/// </summary>
public sealed class RuleValidationException : FirebirdParseException
{
    public string? RuleName { get; init; }
    public string? SampleData { get; init; }
    
    public RuleValidationException(string message, string? ruleName = null) 
        : base(message)
    {
        RuleName = ruleName;
    }
}

/// <summary>
/// Несовпадение версии схемы правил.
/// </summary>
public sealed class SchemaVersionException : FirebirdParseException
{
    public int ExpectedVersion { get; init; }
    public int ActualVersion { get; init; }
    
    public SchemaVersionException(int expected, int actual) 
        : base($"Schema version mismatch. Expected: {expected}, Actual: {actual}")
    {
        ExpectedVersion = expected;
        ActualVersion = actual;
    }
}