# Models

Доменная модель распарсенного трейса Firebird — всё, что производит парсер и с чем дальше работают фильтры, сортировки, отчёты, карточки и хранилище. Живёт в проекте `FirebirdTraceParser` и **не зависит от Avalonia**: на неё опираются и парсер, и плагины, и UI-проект.

Каталог разбит на четыре подраздела; у каждого — свой подробный README (ссылки ниже). Этот файл — навигационный хаб и «как всё собирается вместе».

---

## Подразделы

| Каталог | Что внутри | Подробно |
|---|---|---|
| **[`Enums`](Enums/README.md)** | `EventType` — словарь типов событий (токен трейса → значение). | [README](Enums/README.md) |
| **[`Events`](Events/README.md)** | Иерархия классов событий: `EventBase` + семейства Statement/Procedure/Trigger + Trace/Attach/Detach/Error. | [README](Events/README.md) |
| **[`ValueObjects`](ValueObjects/README.md)** | Вложенные данные событий: `AttachmentInfo`, `TransactionInfo`, `PerformanceInfo`, `PerformanceTable`, `SqlParameters`, `ErrorLines`, `TraceSessionInfo`. | [README](ValueObjects/README.md) |
| **[`Results`](Results/README.md)** | Результат разбора: `ParsingResult<T>` (события + предупреждения), `ParsingWarning`, `WarningSeverity`. | [README](Results/README.md) |

---

## Как это собирается вместе

```
файл трейса
   │  TraceLogParser + DefaultEventHandler   (../Parsing)
   ▼
ParsingResult<EventBase>                       ← Results/
   │
   ├─ Events : IReadOnlyList<EventBase>         ← Events/
   │     • конкретный класс выбирается по типу  ← Enums/ (EventType)
   │     • состоит из вложенных данных          ← ValueObjects/
   │
   └─ Warnings : IReadOnlyList<ParsingWarning>  ← Results/ (пропущенные/сбойные блоки)
```

Цепочка одного события: строка `event_type` из заголовка блока → [`EventType`](Enums/README.md) (через карту в `DefaultEventHandler`) → соответствующий класс из [`Events`](Events/README.md) → наполняется объектами из [`ValueObjects`](ValueObjects/README.md). Все события файла + предупреждения о непонятых блоках упаковываются в [`ParsingResult`](Results/README.md).

---

## Сквозные принципы (детали — в под-README)

- **Иммутабельность.** Все модели — `required … { get; init; }`, заполняются парсером при создании и дальше не меняются. Value-объекты — `record` (значимое равенство), кроме `AttachmentInfo` (`class`, переиспользуется из пула по id).
- **Атрибуты → автоматическая интеграция с UI.** Свойства помечены [`[SortableField]`/`[FilterableField]`](../Attributes/README.md); поле сразу доступно в фильтрах/сортировках/дизайнере отчётов по вложенному пути (`Attachment.User`, `Performance.ExecuteMs`). Тип контрола фильтра — [`FilterType`](../Enums/README.md).
- **Экономия памяти.** Строки и повторяющиеся объекты (`AttachmentInfo`/`TraceSessionInfo`) интернируются на разбор — см. [`Infrastructure/Caching`](../Infrastructure/Caching).
- **Персистентность.** События плющатся в одну таблицу (`event`, тип = `(int)EventType`), словари дедупятся (`attachment`/`sql_text`), коллекции — в дочерние таблицы — см. [`EventStoreService`](../../FirebirdTraceAnalyzer/Services/Persistence/EventStoreService.cs).

---

## Связанные разделы

- [`Parsing/Engine`](../Parsing/Engine/TraceLogParser.cs) — где модель создаётся (`TraceLogParser`, `ParseOptions`, `ValidationMode`).
- [`Parsing/Handlers/DefaultEventHandler.cs`](../Parsing/Handlers/DefaultEventHandler.cs) — маппинг типа и наполнение событий.
- [`Attributes`](../Attributes/README.md) — `[SortableField]`/`[FilterableField]`.
