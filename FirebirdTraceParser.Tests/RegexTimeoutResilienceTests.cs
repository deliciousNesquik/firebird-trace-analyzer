using System.Text.RegularExpressions;
using FirebirdTraceParser.Models.Results;
using FirebirdTraceParser.Parsing.Engine;
using static FirebirdTraceParser.Tests.TestSupport;

namespace FirebirdTraceParser.Tests;

/// <summary>
/// Недоверенная строка, вызывающая катастрофический бэктрекинг в block_header, не должна ронять
/// весь разбор: движок обязан деградировать до предупреждения и продолжить.
/// </summary>
public sealed class RegexTimeoutResilienceTests
{
    // Классический ReDoS-паттерн с крошечным таймаутом.
    private static IReadOnlyDictionary<string, Regex> EvilRules() => new Dictionary<string, Regex>
    {
        ["block_header"] = new(@"^(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(1))
    };

    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ftp_evil_{Guid.NewGuid():N}.log");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void CatastrophicLine_DoesNotCrashParse_ButWarns()
    {
        var parser = new TraceLogParser(EvilRules(), NewHandler(), Logger);
        var path = TempFile(new string('a', 60) + "!\n"); // никогда не матчится → экспоненциальный бэктрекинг
        try
        {
            ParsingResult<Models.Events.EventBase> result = parser.ParseFile(path); // не должно бросить
            Assert.Empty(result.Events);
            Assert.Contains(result.Warnings, w => w.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CatastrophicLine_StrictMode_IsError()
    {
        var parser = new TraceLogParser(EvilRules(), NewHandler(), Logger);
        var path = TempFile(new string('a', 60) + "!\n");
        try
        {
            var result = parser.ParseFile(path, new ParseOptions { ValidationMode = ValidationMode.Strict });
            Assert.True(result.HasErrors); // в строгом режиме таймаут — это Error
        }
        finally { File.Delete(path); }
    }
}
