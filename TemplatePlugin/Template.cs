using FirebirdTraceAnalyzer.Interfaces.Plugins;
using FirebirdTraceAnalyzer.Services.Sorting;
using FirebirdTraceParser.Models.Events;

namespace TemplatePlugin;

public class TemplatePlugin: ISortPlugin
{
    public string Id => "Template_plugin";
    public string Name => "Template (Plugin)";
    public string Author => "system";
    public string Version => "1.0.0";
    
    public IEnumerable<SortDescriptor> GetSorts()
    {
        yield return new SortDescriptor(
            "template_sorting",
            "Template (Plugin)",
            TemplateComparer,
            false,
            "Analytics",
            2);
    }
    
    private int TemplateComparer(EventBase a, EventBase b, bool descending)
    {
        return 0;
    }
}