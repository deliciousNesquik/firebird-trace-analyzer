using System.Text;
using FirebirdTraceParser.Parsing.Engine;
using static FirebirdTraceParser.Tests.TestSupport;

namespace FirebirdTraceParser.Tests;

public sealed class BlockLimitTests
{
    private static TraceLogParser NewParser() => new(Rules, NewHandler(), Logger);

    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ftp_limit_{Guid.NewGuid():N}.log");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void HugeBodyWithoutNewHeader_Truncated_NotBuffered()
    {
        var sb = new StringBuilder();
        sb.AppendLine(HeaderLine("ATTACH_DATABASE"));
        for (var i = 0; i < 10_000; i++) sb.AppendLine($"junk body line {i}");
        var path = TempFile(sb.ToString());
        try
        {
            var result = NewParser().ParseFile(path, new ParseOptions { MaxBlockLines = 100 });
            Assert.Contains(result.Warnings, w => w.Message.Contains("MaxBlockLines"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void NormalBlock_UnderDefaultLimit_NotTruncated()
    {
        var sb = new StringBuilder();
        sb.AppendLine(HeaderLine("ATTACH_DATABASE"));
        sb.AppendLine(AttachmentLine);
        sb.AppendLine(ProcessLine);
        var path = TempFile(sb.ToString());
        try
        {
            var result = NewParser().ParseFile(path); // дефолтный лимит 200k — не срабатывает
            Assert.Single(result.Events);
            Assert.DoesNotContain(result.Warnings, w => w.Message.Contains("MaxBlockLines"));
        }
        finally { File.Delete(path); }
    }
}
