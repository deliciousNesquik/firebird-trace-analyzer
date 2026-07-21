using FirebirdTraceAnalyzer.Services;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// S3: имя файла приходит с сервера (недоверенное). Гарантия безопасности — итоговый путь ВСЕГДА
/// внутри каталога загрузки. Traversal-компоненты обрезаются до базового имени; пустое/'.'/'..'
/// отклоняются. Проверяем нормальные имена и абсурдно-вредоносные.
/// </summary>
public sealed class RemoteFileServicePathTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "fta_dl_root");
    private static string RootPrefix => Path.GetFullPath(Root) + Path.DirectorySeparatorChar;

    [Theory]
    [InlineData("trace.log")]
    [InlineData("my.trace")]
    [InlineData("2026-07-21.trc")]
    public void NormalNames_StayInsideRoot(string name)
    {
        var result = RemoteFileService.ResolveSafeLocalPath(Root, name);
        Assert.Equal(Path.Combine(Path.GetFullPath(Root), name), result);
    }

    [Theory]
    [InlineData("../evil.log", "evil.log")]
    [InlineData("../../../../etc/passwd", "passwd")]
    [InlineData("/etc/shadow", "shadow")]
    [InlineData("subdir/inner.log", "inner.log")]
    [InlineData("a/b/c/only-name.log", "only-name.log")]
    public void TraversalNames_AreFlattenedIntoRoot(string malicious, string expectedBase)
    {
        // Ключевое свойство безопасности: результат всегда внутри корня и равен корень/базовое-имя.
        var result = RemoteFileService.ResolveSafeLocalPath(Root, malicious);
        Assert.StartsWith(RootPrefix, result, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(Path.GetFullPath(Root), expectedBase), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    public void EmptyOrDotNames_AreRejected(string name)
    {
        var ex = Record.Exception(() => RemoteFileService.ResolveSafeLocalPath(Root, name));
        Assert.IsType<InvalidOperationException>(ex);
    }
}
