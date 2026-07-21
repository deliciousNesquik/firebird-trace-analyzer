using System.Text.RegularExpressions;
using FirebirdTraceParser.Exceptions;
using FirebirdTraceParser.Parsing.Rules;
using Microsoft.Extensions.Caching.Memory;

namespace FirebirdTraceParser.Tests;

public sealed class RuleLoaderTests
{
    private static JsonRuleLoader NewLoader() =>
        new(new MemoryCache(new MemoryCacheOptions()), TestSupport.Logger);

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ftp_rules_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void LoadsBundledRules()
    {
        var rules = NewLoader().LoadRules(TestSupport.RulesPath);
        Assert.Contains("block_header", rules.Keys);
        Assert.Contains("attachment", rules.Keys);
        Assert.Contains("performance", rules.Keys);
    }

    [Fact]
    public void MissingFile_Throws()
    {
        var loader = NewLoader();
        Assert.Throws<RuleValidationException>(() =>
            loader.LoadRules(Path.Combine(Path.GetTempPath(), "definitely_missing_" + Guid.NewGuid().ToString("N") + ".json")));
    }

    [Fact]
    public void WrongSchemaVersion_Throws()
    {
        var path = WriteTemp("""{ "schemaVersion": 999, "rules": {} }""");
        try { Assert.Throws<SchemaVersionException>(() => NewLoader().LoadRules(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MissingRequiredGroup_Throws()
    {
        var path = WriteTemp("""
        { "schemaVersion": 1, "rules": {
            "r": { "pattern": "^(?<a>\\d+)$", "flags": [], "requiredGroups": ["b"], "sample": "123" } } }
        """);
        try
        {
            var ex = Assert.Throws<RuleValidationException>(() => NewLoader().LoadRules(path));
            Assert.Equal("r", ex.RuleName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SampleMismatch_Throws()
    {
        var path = WriteTemp("""
        { "schemaVersion": 1, "rules": {
            "r": { "pattern": "^\\d+$", "flags": [], "requiredGroups": [], "sample": "not-a-number" } } }
        """);
        try
        {
            var ex = Assert.Throws<RuleValidationException>(() => NewLoader().LoadRules(path));
            Assert.Equal("not-a-number", ex.SampleData);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void UnknownFlag_Throws()
    {
        var path = WriteTemp("""
        { "schemaVersion": 1, "rules": {
            "r": { "pattern": "^\\d+$", "flags": ["Bogus"], "requiredGroups": [], "sample": "1" } } }
        """);
        try { Assert.Throws<RuleValidationException>(() => NewLoader().LoadRules(path)); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidRegexPattern_PreservesInnerException()
    {
        var path = WriteTemp("""
        { "schemaVersion": 1, "rules": {
            "r": { "pattern": "(unbalanced", "flags": [], "requiredGroups": [], "sample": "x" } } }
        """);
        try
        {
            var ex = Assert.Throws<RuleValidationException>(() => NewLoader().LoadRules(path));
            Assert.NotNull(ex.InnerException); // первопричина (RegexParseException) не потеряна
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CompiledRules_HaveTimeoutSet()
    {
        var rules = NewLoader().LoadRules(TestSupport.RulesPath);
        // Каждый паттерн обязан иметь конечный таймаут — защита от ReDoS на недоверенном вводе.
        Assert.All(rules.Values, rx => Assert.NotEqual(Regex.InfiniteMatchTimeout, rx.MatchTimeout));
    }

    [Fact]
    public void RegexTimeout_TakenFromParseOptions()
    {
        var loader = new JsonRuleLoader(new MemoryCache(new MemoryCacheOptions()), TestSupport.Logger,
            new FirebirdTraceParser.Parsing.Engine.ParseOptions { RegexTimeout = TimeSpan.FromMilliseconds(37) });
        var rules = loader.LoadRules(TestSupport.RulesPath);
        Assert.All(rules.Values, rx => Assert.Equal(TimeSpan.FromMilliseconds(37), rx.MatchTimeout));
    }
}
