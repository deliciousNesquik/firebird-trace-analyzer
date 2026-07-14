namespace FirebirdTraceParser.Models.ValueObjects;

/// <summary>
/// Описание одного параметра SQL‑запроса.
/// </summary>
public sealed record SqlParameters
{

    /// <summary>Имя параметра (например, param0, param1)</summary>
    public required string Name { get; init; }

    /// <summary>Тип параметра в терминах Firebird (bigint, varchar, etc.)</summary>
    public required string Dtype { get; init; }

    /// <summary>Значение параметра в строковом представлении</summary>
    public required string Value { get; init; }

    /// <summary>Человекочитаемое представление: "&lt;имя&gt; (&lt;тип&gt;) = &lt;значение&gt;".</summary>
    public override string ToString() => $"{Name} ({Dtype}) = {Value}";
}