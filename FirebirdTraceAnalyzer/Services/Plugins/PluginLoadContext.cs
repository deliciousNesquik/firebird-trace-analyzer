using System.Reflection;
using System.Runtime.Loader;

namespace FirebirdTraceAnalyzer.Services.Plugins;

/// <summary>Изолированный контекст загрузки сборки плагина</summary>
internal class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}