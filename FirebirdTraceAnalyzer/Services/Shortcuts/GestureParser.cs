using Avalonia.Input;

namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>
/// Парсер логических сочетаний («Primary+Shift+O»). Свой, а не Avalonia KeyGesture.Parse:
/// тот кидает исключения на неизвестных токенах и молча возвращает Key.None для строки
/// из одних модификаторов — оба поведения неприемлемы для пользовательского ввода.
/// Разбор всегда нормализует результат (Meta/Cmd/Win → Primary, избыточный Control при
/// Primary отбрасывается), поэтому canonical-строки round-trip-стабильны.
/// </summary>
public static class GestureParser
{
    private static readonly Dictionary<string, LogicalModifiers> ModifierTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["primary"] = LogicalModifiers.Primary,
            ["ctrl"] = LogicalModifiers.Control,
            ["control"] = LogicalModifiers.Control,
            ["shift"] = LogicalModifiers.Shift,
            ["alt"] = LogicalModifiers.Alt,
            ["meta"] = LogicalModifiers.Meta,
            ["cmd"] = LogicalModifiers.Meta,
            ["command"] = LogicalModifiers.Meta,
            ["win"] = LogicalModifiers.Meta,
        };

    /// <summary>Разбирает строку в <see cref="GestureSpec"/>. Ничего не бросает.</summary>
    public static bool TryParse(string? text, out GestureSpec spec, out string? error)
    {
        spec = new GestureSpec(Key.None, LogicalModifiers.None);
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Пустая строка сочетания";
            return false;
        }

        var modifiers = LogicalModifiers.None;
        Key key = Key.None;
        var hasKey = false;

        foreach (var raw in text.Split('+'))
        {
            var token = raw.Trim();

            if (token.Length == 0)
            {
                error = $"Пустой токен в «{text}» (для клавиши «+» используйте OemPlus)";
                return false;
            }

            if (ModifierTokens.TryGetValue(token, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (Enum.TryParse<Key>(token, ignoreCase: true, out var parsed) && parsed != Key.None)
            {
                if (hasKey)
                {
                    error = $"Несколько клавиш в «{text}»";
                    return false;
                }

                key = parsed;
                hasKey = true;
                continue;
            }

            error = $"Неизвестный токен «{token}»";
            return false;
        }

        if (!hasKey)
        {
            error = $"Нет клавиши в «{text}»";
            return false;
        }

        spec = Normalize(new GestureSpec(key, modifiers));
        return true;
    }

    /// <summary>
    /// Нормализация пользовательского ввода: физические Meta/Cmd/Win трактуются как Primary,
    /// избыточный Control рядом с Primary отбрасывается (Primary уже разрешается в Control
    /// на Win/Linux и в Meta на macOS).
    /// </summary>
    public static GestureSpec Normalize(GestureSpec spec)
    {
        var modifiers = spec.Modifiers;

        if ((modifiers & LogicalModifiers.Meta) != 0)
        {
            modifiers |= LogicalModifiers.Primary;
            modifiers &= ~LogicalModifiers.Meta;
        }

        if ((modifiers & LogicalModifiers.Primary) != 0)
            modifiers &= ~LogicalModifiers.Control;

        return spec with { Modifiers = modifiers };
    }

    /// <summary>Каноничная строка: Primary, Ctrl, Shift, Alt, Meta — в этом порядке, затем клавиша.</summary>
    public static string ToCanonicalString(GestureSpec spec)
    {
        var parts = new List<string>(6);

        if ((spec.Modifiers & LogicalModifiers.Primary) != 0) parts.Add("Primary");
        if ((spec.Modifiers & LogicalModifiers.Control) != 0) parts.Add("Ctrl");
        if ((spec.Modifiers & LogicalModifiers.Shift) != 0) parts.Add("Shift");
        if ((spec.Modifiers & LogicalModifiers.Alt) != 0) parts.Add("Alt");
        if ((spec.Modifiers & LogicalModifiers.Meta) != 0) parts.Add("Meta");

        parts.Add(spec.Key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>
    /// Разрешает логический спецификатор в физический <see cref="KeyGesture"/> для платформы:
    /// Primary → Meta на macOS и Control на остальных. Возвращает null для Key.None.
    /// </summary>
    public static KeyGesture? ToKeyGesture(GestureSpec spec, ShortcutPlatform platform)
    {
        if (spec.Key == Key.None)
            return null;

        var modifiers = KeyModifiers.None;

        if ((spec.Modifiers & LogicalModifiers.Primary) != 0)
            modifiers |= platform == ShortcutPlatform.MacOS ? KeyModifiers.Meta : KeyModifiers.Control;

        if ((spec.Modifiers & LogicalModifiers.Control) != 0) modifiers |= KeyModifiers.Control;
        if ((spec.Modifiers & LogicalModifiers.Shift) != 0) modifiers |= KeyModifiers.Shift;
        if ((spec.Modifiers & LogicalModifiers.Alt) != 0) modifiers |= KeyModifiers.Alt;
        if ((spec.Modifiers & LogicalModifiers.Meta) != 0) modifiers |= KeyModifiers.Meta;

        return new KeyGesture(spec.Key, modifiers);
    }
}
