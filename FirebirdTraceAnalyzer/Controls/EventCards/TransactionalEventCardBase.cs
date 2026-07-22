using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

/// <summary>
/// Базовый класс карточек событий, выполняемых в транзакции (statement/procedure/trigger). Держит
/// общий блок свойств транзакции. Attach/Detach имеют подключение, но не транзакцию, поэтому
/// наследуются от <see cref="ConnectedEventCardBase"/>. Значения по умолчанию сохранены прежними.
/// </summary>
public abstract class TransactionalEventCardBase : ConnectedEventCardBase
{
    public static readonly StyledProperty<long> TransactionIdProperty =
        AvaloniaProperty.Register<TransactionalEventCardBase, long>(nameof(TransactionId), 0);

    public static readonly StyledProperty<string> IsolationLevelProperty =
        AvaloniaProperty.Register<TransactionalEventCardBase, string>(nameof(IsolationLevel), "<not set>");

    public static readonly StyledProperty<string> ConsistencyModeProperty =
        AvaloniaProperty.Register<TransactionalEventCardBase, string>(nameof(ConsistencyMode), "<not set>");

    public static readonly StyledProperty<string> LockModeProperty =
        AvaloniaProperty.Register<TransactionalEventCardBase, string>(nameof(LockMode), "<not set>");

    public static readonly StyledProperty<string> AccessModeProperty =
        AvaloniaProperty.Register<TransactionalEventCardBase, string>(nameof(AccessMode), "<not set>");

    public long TransactionId { get => GetValue(TransactionIdProperty); set => SetValue(TransactionIdProperty, value); }
    public string IsolationLevel { get => GetValue(IsolationLevelProperty); set => SetValue(IsolationLevelProperty, value); }
    public string ConsistencyMode { get => GetValue(ConsistencyModeProperty); set => SetValue(ConsistencyModeProperty, value); }
    public string LockMode { get => GetValue(LockModeProperty); set => SetValue(LockModeProperty, value); }
    public string AccessMode { get => GetValue(AccessModeProperty); set => SetValue(AccessModeProperty, value); }
}
