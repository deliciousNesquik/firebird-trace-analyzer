using FirebirdTraceParser.Infrastructure.Caching;
using FirebirdTraceParser.Models.ValueObjects;

namespace FirebirdTraceParser.Tests;

public sealed class ParsingContextTests
{
    [Fact]
    public void Intern_ReturnsSameReferenceForEqualStrings()
    {
        var ctx = new ParsingContext();
        var a = ctx.Intern(new string('x', 10));
        var b = ctx.Intern(new string('x', 10));
        Assert.Same(a, b); // дедупликация: одна и та же ссылка
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Intern_NullOrEmpty_ReturnsEmpty(string? value)
    {
        Assert.Same(string.Empty, new ParsingContext().Intern(value));
    }

    [Fact]
    public void Intern_LongStrings_NotDeduplicated()
    {
        var ctx = new ParsingContext();
        var a = new string('x', 200);
        var b = new string('x', 200);
        // Длинные строки не интернируются: словарь не раздувается, возвращается исходный экземпляр.
        Assert.NotSame(ctx.Intern(a), ctx.Intern(b));
        Assert.Same(a, ctx.Intern(a));
    }

    [Fact]
    public void InternSession_DeduplicatesById()
    {
        var ctx = new ParsingContext();
        Assert.Same(ctx.InternSession(5), ctx.InternSession(5));
        Assert.NotSame(ctx.InternSession(5), ctx.InternSession(6));
    }

    [Fact]
    public void Attachment_CacheRoundTrips()
    {
        var ctx = new ParsingContext();
        Assert.False(ctx.TryGetAttachment(1, out _));

        var info = new AttachmentInfo
        {
            AttachmentId = 1, DatabasePath = "db", User = "u", Role = "r",
            Charset = "c", Protocol = "p", Address = "a", Port = 1
        };
        Assert.Same(info, ctx.AddAttachment(info));
        Assert.True(ctx.TryGetAttachment(1, out var got));
        Assert.Same(info, got);
    }
}
