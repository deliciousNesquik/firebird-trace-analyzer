namespace FirebirdTraceAnalyzer.Interfaces.Plugins;

/// <summary>Базовый интерфейс для любого плагина системы</summary>
public interface IAnalyzerPlugin
{
    string Id { get; }
    string Name { get; }
    string Author { get; }
    string Version { get; }
}