using System.Text.RegularExpressions;
using FirebirdTraceParser.Parsing.Utils;

namespace FirebirdTraceParser.Tests;

public sealed class ParsingExtensionsTests
{
    private static readonly Regex Rx =
        new(@"(?<word>[a-z]+)?(?<num>\d+)?", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static Match M(string input) => Rx.Match(input);

    [Fact]
    public void GetGroupValue_ReturnsValue_WhenMatched()
        => Assert.Equal("abc", M("abc").GetGroupValue("word"));

    [Fact]
    public void GetGroupValue_ReturnsDefault_WhenNotMatched()
        => Assert.Equal("fallback", M("123").GetGroupValue("word", "fallback"));

    [Fact]
    public void GetGroupValue_ReturnsDefault_ForUnknownGroup()
        => Assert.Equal("", M("abc").GetGroupValue("nope"));

    [Fact]
    public void GetGroupInt_Parses()
        => Assert.Equal(42, M("42").GetGroupInt("num"));

    [Fact]
    public void GetGroupInt_DefaultOnMissing()
        => Assert.Equal(-1, M("abc").GetGroupInt("num", -1));

    [Fact]
    public void GetGroupLong_Parses()
        => Assert.Equal(9999999999L, M("9999999999").GetGroupLong("num"));

    [Fact]
    public void GetGroupInt_DefaultOnOverflow()
        => Assert.Equal(0, M("99999999999999999999").GetGroupInt("num")); // > int.MaxValue → default
}
