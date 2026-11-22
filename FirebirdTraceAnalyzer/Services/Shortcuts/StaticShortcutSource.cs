namespace FirebirdTraceAnalyzer.Services.Shortcuts;

/// <summary>Источник статичных действий ядра (каталог). Changed никогда не срабатывает.</summary>
public sealed class StaticShortcutSource : IShortcutActionSource
{
    public StaticShortcutSource(ShortcutAction[] actions) => Actions = actions;

    private ShortcutAction[] Actions { get; }

    public string Owner => ShortcutCatalog.OwnerCore;

    public IReadOnlyList<ShortcutAction> Snapshot() => Actions;

#pragma warning disable CS0067 // У статичного каталога набор действий неизменен
    public event EventHandler? Changed;
#pragma warning restore CS0067
}
