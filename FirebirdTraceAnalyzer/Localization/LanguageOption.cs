namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Represents a language option with its code and name.
/// </summary>
/// <param name="Code">The language code.</param>
/// <param name="Name">The name of the language.</param>
public sealed record LanguageOption(string Code, string Name);
