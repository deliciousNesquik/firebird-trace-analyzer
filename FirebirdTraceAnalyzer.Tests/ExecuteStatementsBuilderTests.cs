using FirebirdTraceAnalyzer.Core;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// L16: ExecuteStatementsBuilder.Build null-safe — не падает NRE на null-списке параметров
/// (у карточек ParamsProperty без дефолта = null) или null-элементе.
/// </summary>
public sealed class ExecuteStatementsBuilderTests
{
    [Fact]
    public void NullParameters_ReturnsSqlUnchanged()
        => Assert.Equal("SELECT 1", ExecuteStatementsBuilder.Build("SELECT 1", null));

    [Fact]
    public void EmptyParameters_ReturnsSqlUnchanged()
        => Assert.Equal("SELECT ?", ExecuteStatementsBuilder.Build("SELECT ?", new List<SqlParameters?>()));

    [Fact]
    public void NullElement_LeftAsPlaceholder_NoThrow()
    {
        var result = ExecuteStatementsBuilder.Build("SELECT ?", new List<SqlParameters?> { null });
        Assert.Contains("?", result); // null-параметр не подставлен, но метод не упал
    }

    [Fact]
    public void RealParameter_IsSubstituted()
    {
        var result = ExecuteStatementsBuilder.Build("SELECT ?",
            new List<SqlParameters?> { new() { Name = "p0", Dtype = "integer", Value = "42" } });
        Assert.Contains("42", result);
    }
}
