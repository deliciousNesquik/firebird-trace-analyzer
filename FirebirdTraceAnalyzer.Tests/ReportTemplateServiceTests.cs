using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Reports;

namespace FirebirdTraceAnalyzer.Tests;

/// <summary>
/// T4: сервис шаблонов работает с внедрённым каталогом (шов). Заодно S6: Id с разделителями пути
/// не выводит файл за каталог шаблонов.
/// </summary>
public sealed class ReportTemplateServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fta_tpl_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ReportTemplate Custom(string id, string name) =>
        new() { Id = id, Name = name, IsBuiltIn = false };

    [Fact]
    public async Task Save_Get_Delete_RoundTrip()
    {
        var svc = new ReportTemplateService(_dir);
        await svc.SaveTemplateAsync(Custom("id-1", "My Report"));

        var byId = await svc.GetTemplateByIdAsync("id-1");
        Assert.NotNull(byId);
        Assert.Equal("My Report", byId!.Name);

        var all = await svc.GetAllTemplatesAsync();
        Assert.Contains(all, t => t.Id == "id-1");

        await svc.DeleteTemplateAsync("id-1");
        Assert.Null(await svc.GetTemplateByIdAsync("id-1"));
    }

    [Fact]
    public async Task TemplateIdWithTraversal_StaysInsideDirectory()
    {
        var svc = new ReportTemplateService(_dir);
        await svc.SaveTemplateAsync(Custom("../../evil", "Bad"));

        // Все созданные файлы обязаны лежать внутри каталога шаблонов (Id санирован).
        foreach (var file in Directory.GetFiles(_dir, "*.json", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(file);
            Assert.StartsWith(Path.GetFullPath(_dir) + Path.DirectorySeparatorChar, full, StringComparison.Ordinal);
        }
        // И родительский каталог не получил посторонних .json от этого сохранения.
        var parent = Directory.GetParent(_dir)!.FullName;
        Assert.DoesNotContain(Directory.GetFiles(parent, "*evil*.json"), _ => true);
    }
}
