using Avalonia.Input;
using FirebirdTraceAnalyzer.Services.Shortcuts;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// Парсер логических жестов: каноничные строки, нормализация физических Meta/Cmd/Win → Primary,
/// толерантность к мусору (никогда не бросает) и round-trip стабильность.
/// </summary>
public sealed class GestureParserTests
{
    [Theory]
    [InlineData("Primary+O")]
    [InlineData("primary+o")]           // регистр не важен
    [InlineData("Cmd+O")]               // физический cmd нормализуется в Primary
    [InlineData("Meta+O")]
    [InlineData("Win+O")]
    public void Parse_PrimaryVariants_YieldSameSpec(string text)
    {
        var ok = GestureParser.TryParse(text, out var spec, out var error);

        Assert.True(ok, error);
        Assert.Equal(Key.O, spec.Key);
        Assert.Equal(LogicalModifiers.Primary, spec.Modifiers);
        Assert.Equal("Primary+O", spec.ToString());
    }

    [Fact]
    public void Parse_ShiftCombo_PreservesOrder()
    {
        var ok = GestureParser.TryParse("cmd+shift+r", out var spec, out _);

        Assert.True(ok);
        Assert.Equal(Key.R, spec.Key);
        Assert.Equal(LogicalModifiers.Primary | LogicalModifiers.Shift, spec.Modifiers);
        Assert.Equal("Primary+Shift+R", spec.ToString());
    }

    [Theory]
    [InlineData("Primary+OemComma")]
    [InlineData("Primary+D1")]
    [InlineData("Primary+Escape")]
    [InlineData("Ctrl+OemPlus")]        // клавиша «+»: разбиение по '+' должно работать
    public void Parse_KeyEnumNames_RoundTrip(string text)
    {
        var ok = GestureParser.TryParse(text, out var spec, out var error);

        Assert.True(ok, error);
        Assert.Equal(text, spec.ToString());
    }

    [Fact]
    public void Parse_PrimaryWithExplicitControl_DropsRedundantControl()
    {
        var ok = GestureParser.TryParse("Primary+Ctrl+O", out var spec, out _);

        Assert.True(ok);
        Assert.Equal(LogicalModifiers.Primary, spec.Modifiers);
        Assert.Equal("Primary+O", spec.ToString());
    }

    [Theory]
    [InlineData("", "пуст")]
    [InlineData("   ", "пуст")]
    [InlineData("Ctrl", "нет клавиши")]     // только модификатор
    [InlineData("Primary+", "пустой токен")]
    [InlineData("Ctrl++", "пустой токен")]
    [InlineData("Ctrl+A+B", "несколько клавиш")]
    [InlineData("Ctrl+NotAKey", "неизвестный токен")]
    public void Parse_Garbage_ReturnsFalseWithoutThrowing(string text, string _)
    {
        var ok = GestureParser.TryParse(text, out var spec, out var error);

        Assert.False(ok);
        Assert.Null(error is null ? "" : null); // сообщение всегда присутствует при false
        Assert.NotEmpty(error!);
        Assert.Equal(Key.None, spec.Key);
    }

    [Fact]
    public void Parse_Null_ReturnsFalse()
    {
        var ok = GestureParser.TryParse(null, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(ShortcutPlatform.MacOS, KeyModifiers.Meta)]
    [InlineData(ShortcutPlatform.Windows, KeyModifiers.Control)]
    [InlineData(ShortcutPlatform.Linux, KeyModifiers.Control)]
    public void ToKeyGesture_Primary_ResolvesPerPlatform(ShortcutPlatform platform, KeyModifiers expected)
    {
        var ok = GestureParser.TryParse("Primary+Shift+O", out var spec, out _);
        Assert.True(ok);

        var gesture = GestureParser.ToKeyGesture(spec, platform);

        Assert.NotNull(gesture);
        Assert.Equal(Key.O, gesture.Key);
        Assert.Equal(expected | KeyModifiers.Shift, gesture.KeyModifiers);
    }

    [Fact]
    public void ToKeyGesture_KeyNone_ReturnsNull()
    {
        var gesture = GestureParser.ToKeyGesture(new GestureSpec(Key.None, LogicalModifiers.Primary), ShortcutPlatform.MacOS);

        Assert.Null(gesture);
    }

    [Fact]
    public void Canonical_RoundTrip_IsStable()
    {
        var ok = GestureParser.TryParse("Cmd+Shift+W", out var parsed, out _);
        Assert.True(ok);

        var canonical = parsed.ToString();
        var ok2 = GestureParser.TryParse(canonical, out var reparsed, out _);

        Assert.True(ok2);
        Assert.Equal(parsed, reparsed);
        Assert.Equal(canonical, reparsed.ToString());
    }
}
