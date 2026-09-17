using FirebirdTraceAnalyzer.Controls.EventCards;

namespace FirebirdTraceAnalyzer.Controls.EventCards.Compact;

/// <summary>
/// Компактная (однострочная) карточка события подключения к БД.
/// Переиспользует свойства <see cref="ConnectedEventCardBase"/>; в строке показываются
/// только тип, идентификатор подключения, пользователь, путь БД и время.
/// </summary>
public class AttachDatabaseCompactCard : ConnectedEventCardBase
{
}
