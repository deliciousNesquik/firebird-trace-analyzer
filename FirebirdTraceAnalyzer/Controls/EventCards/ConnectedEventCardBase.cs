using Avalonia;

namespace FirebirdTraceAnalyzer.Controls.EventCards;

/// <summary>
/// Базовый класс карточек событий, привязанных к подключению к БД (attach/detach и все
/// statement/procedure/trigger). Держит общий блок свойств подключения, чтобы не дублировать их
/// регистрацию в каждой карточке. Значения по умолчанию сохранены прежними.
/// Карточки без подключения (TraceInit/TraceFini, Error) наследуются напрямую от <see cref="EventCardBase"/>.
/// </summary>
public abstract class ConnectedEventCardBase : EventCardBase
{
    public static readonly StyledProperty<long> AttachmentIdProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, long>(nameof(AttachmentId), 0);

    public static readonly StyledProperty<string> DatabasePathProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, string>(nameof(DatabasePath), "<not set>");

    public static readonly StyledProperty<string> UserProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, string>(nameof(User), "<not set>");

    public static readonly StyledProperty<string> RoleProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, string>(nameof(Role), "<not set>");

    public static readonly StyledProperty<string> CharsetProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, string>(nameof(Charset), "<not set>");

    public static readonly StyledProperty<string> ProtocolProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, string>(nameof(Protocol), "<not set>");

    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, string>(nameof(Address), "<not set>");

    public static readonly StyledProperty<int> PortProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, int>(nameof(Port), 0);

    public static readonly StyledProperty<string> ProcessPathProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, string>(nameof(ProcessPath), "<not set>");

    public static readonly StyledProperty<int> ProcessIdProperty =
        AvaloniaProperty.Register<ConnectedEventCardBase, int>(nameof(ProcessId), 0);

    public long AttachmentId { get => GetValue(AttachmentIdProperty); set => SetValue(AttachmentIdProperty, value); }
    public string DatabasePath { get => GetValue(DatabasePathProperty); set => SetValue(DatabasePathProperty, value); }
    public string User { get => GetValue(UserProperty); set => SetValue(UserProperty, value); }
    public string Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public string Charset { get => GetValue(CharsetProperty); set => SetValue(CharsetProperty, value); }
    public string Protocol { get => GetValue(ProtocolProperty); set => SetValue(ProtocolProperty, value); }
    public string Address { get => GetValue(AddressProperty); set => SetValue(AddressProperty, value); }
    public int Port { get => GetValue(PortProperty); set => SetValue(PortProperty, value); }
    public string ProcessPath { get => GetValue(ProcessPathProperty); set => SetValue(ProcessPathProperty, value); }
    public int ProcessId { get => GetValue(ProcessIdProperty); set => SetValue(ProcessIdProperty, value); }
}
