using Avalonia.Input;

namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Логические модификаторы сочетания. Primary — платформонезависимый «главный» модификатор:
/// разрешается в Ctrl на Windows/Linux и в Meta (⌘) на macOS. Остальные — физические.
/// </summary>
[Flags]
public enum LogicalModifiers
{
    None = 0,
    Primary = 1,
    Control = 2,
    Shift = 4,
    Alt = 8,
    Meta = 16,
}

/// <summary>
/// Платформонезависимое сочетание клавиш: логические модификаторы + клавиша.
/// Каноничная строка — <see cref="GestureParser.ToCanonicalString"/> (например, «Primary+Shift+O»).
/// </summary>
public sealed record GestureSpec(Key Key, LogicalModifiers Modifiers)
{
    public override string ToString() => GestureParser.ToCanonicalString(this);
}

/// <summary>Платформа, для которой разрешается Primary.</summary>
public enum ShortcutPlatform
{
    Windows,
    MacOS,
    Linux,
}
