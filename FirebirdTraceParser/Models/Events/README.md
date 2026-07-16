# Models/Events

Доменная модель одного разобранного события трейса — иерархия классов, которую производит парсер и с которой дальше работают фильтры, сортировки, отчёты, карточки и хранилище.

Каждый класс соответствует значению [`EventType`](../Enums/README.md); какой именно класс создан — определяет [`DefaultEventHandler`](../../Parsing/Handlers/DefaultEventHandler.cs) по типу события.

---

## Иерархия

```
EventBase                                   (Timestamp, TraceId, HexTraceId, EventType)
├─ TraceInitEvent, TraceFinishEvent          → Session
├─ AttachDatabaseEvent, DetachDatabaseEvent  → Attachment
├─ ErrorEvent                                → Attachment, Component, Errors
├─ StatementEventBase                        → Attachment, Transaction?, StatementId?, Sql, Parameters
│   ├─ StatementStartEvent
│   ├─ StatementRestartEvent        (+ RestartCount)
│   ├─ StatementFinishEvent         (+ Performance, PerformanceTable?)
│   └─ FailedStatementFinishEvent   (+ Performance, PerformanceTable?)
├─ ProcedureEventBase                        → Attachment, Transaction, ProcedureName, Parameters
│   ├─ ProcedureStartEvent
│   ├─ ProcedureFinishEvent         (+ Performance, PerformanceTable?)
│   └─ FailedProcedureFinishEvent   (+ Performance, PerformanceTable?)
└─ TriggerEventBase                          → Attachment, Transaction, TriggerName, Table?, Timing?, Event
    ├─ TriggerStartEvent
    ├─ TriggerFinishEvent           (+ Performance, PerformanceTable?)
    └─ FailedTriggerFinishEvent     (+ Performance, PerformanceTable?)
```

Все свойства — `required … { get; init; }` (кроме опциональных `nullable`): объект **иммутабелен** после создания, парсер заполняет его целиком в обработчике. Базовые классы семейств (`*EventBase`) — обычные `public class`, но напрямую не инстанцируются; создаются только листовые `sealed`-типы.

---

## `EventBase` — общие поля

Файл [`EventBase.cs`](EventBase.cs). Есть у **каждого** события.

| Свойство | Тип | Атрибуты (категория «General») |
|---|---|---|
| `Timestamp` | `DateTime` | `[Sortable IsDefault]`, `[Filterable DateTimeRange]` |
| `TraceId` | `int` | `[Sortable]`, `[Filterable StringMultiSelect]` |
| `HexTraceId` | `string` | `[Sortable]`, `[Filterable StringMultiSelect]` |
| `EventType` | [`EventType`](../Enums/README.md) | `[Sortable]`, `[Filterable EnumMultiSelect]` |

Атрибуты `[SortableField]`/`[FilterableField]` — из [`Attributes`](../../Attributes/README.md); именно они (через рефлексию) делают поле доступным в панелях фильтров/сортировок и в конструкторе отчётов. Тип контрола фильтра задаёт [`FilterType`](../../Enums/README.md).

---

## Листовые классы

| Класс | [`EventType`](../Enums/README.md) | Семейство | Доп. поля (сверх базы) | Value-объекты |
|---|---|---|---|---|
| `TraceInitEvent` | `TraceInit` | — (от `EventBase`) | `Session` | `TraceSessionInfo` |
| `TraceFinishEvent` | `TraceFinish` | — | `Session` | `TraceSessionInfo` |
| `AttachDatabaseEvent` | `AttachDatabase` | — | `Attachment` | `AttachmentInfo` |
| `DetachDatabaseEvent` | `DetachDatabase` | — | `Attachment` | `AttachmentInfo` |
| `ErrorEvent` | `Error` | — | `Attachment`, `Component`, `Errors` | `AttachmentInfo`, `ErrorLines[]` |
| `StatementStartEvent` | `ExecuteStatementStart` | Statement | — | |
| `StatementRestartEvent` | `ExecuteStatementRestart` | Statement | `RestartCount` | |
| `StatementFinishEvent` | `ExecuteStatementFinish` | Statement | `Performance`, `PerformanceTable?` | `PerformanceInfo`, `PerformanceTable` |
| `FailedStatementFinishEvent` | `FailedExecuteStatementFinish` | Statement | `Performance`, `PerformanceTable?` | ↑ |
| `ProcedureStartEvent` | `ExecuteProcedureStart` | Procedure | — | |
| `ProcedureFinishEvent` | `ExecuteProcedureFinish` | Procedure | `Performance`, `PerformanceTable?` | ↑ |
| `FailedProcedureFinishEvent` | `FailedExecuteProcedureFinish` | Procedure | `Performance`, `PerformanceTable?` | ↑ |
| `TriggerStartEvent` | `ExecuteTriggerStart` | Trigger | — | |
| `TriggerFinishEvent` | `ExecuteTriggerFinish` | Trigger | `Performance`, `PerformanceTable?` | ↑ |
| `FailedTriggerFinishEvent` | `FailedExecuteTriggerFinish` | Trigger | `Performance`, `PerformanceTable?` | ↑ |

> Значение `EventType.ExecuteProcedureRestart` **не имеет** класса-события (процедуры «рестарт» парсером не производятся) — см. [README по `EventType`](../Enums/README.md).

---

## Семейства и их базы

- **Statement** ([`StatementEvents.cs`](StatementEvents.cs)) — `StatementEventBase`: `Attachment`, **`Transaction?`** (nullable), `StatementId?`, `Sql`, `Parameters`. `Sql` фильтруется как `TextSearch` (подстрока), а не мультиселект — уникальных SQL слишком много.
- **Procedure** ([`ProcedureEvents.cs`](ProcedureEvents.cs)) — `ProcedureEventBase`: `Attachment`, **`Transaction`** (обязателен), `ProcedureName`, `Parameters`.
- **Trigger** ([`TriggerEvents.cs`](TriggerEvents.cs)) — `TriggerEventBase`: `Attachment`, `Transaction`, `TriggerName`, **`Table?`/`Timing?`** (nullable — у DDL-триггеров таблицы/тайминга нет), `Event` (обязателен).
- **Trace / Attach / Detach** ([`TraceEvents.cs`](TraceEvents.cs)) и **Error** ([`ErrorEvent.cs`](ErrorEvent.cs)) наследуются напрямую от `EventBase`.

Общие закономерности:
- `*FinishEvent` и `Failed*FinishEvent` по составу полей **идентичны** (оба несут `Performance` + опциональную `PerformanceTable`); различаются только `EventType` и семантикой (Failed = ошибка при выполнении).
- `PerformanceTable` всегда опциональна (`?`) и наполняется только при `ParseOptions.ParsePerformanceTables = true`.
- Вложенные данные — из [`Models/ValueObjects`](../ValueObjects) (`AttachmentInfo` пулится по id, остальные — новые на событие; детали в [анализе аллокаций/интернировании `Infrastructure/Caching`](../../Infrastructure/Caching)).

---

## Кто потребляет эти классы

1. **Парсер** — создаёт их в [`DefaultEventHandler`](../../Parsing/Handlers/DefaultEventHandler.cs) (по одному `HandleX` на тип), возвращает списком `IReadOnlyList<EventBase>`.
2. **Фильтры/сортировки/отчёты** — через рефлексию по `[SortableField]`/`[FilterableField]` на свойствах (см. [`Attributes`](../../Attributes/README.md)). Значения полей достаёт компилируемый геттер (`EventPropertyAccessor`).
3. **Хранилище** — [`EventStoreService`](../../../FirebirdTraceAnalyzer/Services/Persistence/EventStoreService.cs): **single-table inheritance** — все подтипы плющатся в одну таблицу `event` с nullable-колонками под поля подтипов; тип восстанавливается по `event_type = (int)EventType`.
4. **UI-карточки** — [`Controls/EventCards`](../../../FirebirdTraceAnalyzer/Controls/EventCards) рисуют своё представление на каждый тип события.

---

## Что учесть при изменении модели

- **`required`-поля — контракт с парсером и хранилищем.** Добавление обязательного поля требует: заполнить его в соответствующем `HandleX`, добавить колонку/чтение-запись в `EventStoreService` (иначе round-trip сломается), при необходимости — колонку в отчётах/карточке.
- **Атрибуты полей = автоматическая интеграция с UI.** Пометка свойства `[FilterableField]`/`[SortableField]` сразу включает его в фильтры/сортировки/дизайнер отчётов — без правок UI. Категория группирует поле в панели.
- **Новый тип события** — сначала значение в [`EventType`](../Enums/README.md) и его маппинг/обработчик, затем класс здесь, затем (при необходимости) колонки в сторе и карточка в UI.

---

## Связанные разделы

- [`Models/Enums`](../Enums) — `EventType` (какой класс какому типу соответствует).
- [`Models/ValueObjects`](../ValueObjects) — вложенные данные (`AttachmentInfo`, `TransactionInfo`, `PerformanceInfo`, `PerformanceTable`, `SqlParameters`, `ErrorLines`, `TraceSessionInfo`).
- [`Attributes`](../../Attributes/README.md) — `[SortableField]`/`[FilterableField]`; [`Enums/FilterType`](../../Enums/README.md).
- [`Parsing/Handlers/DefaultEventHandler.cs`](../../Parsing/Handlers/DefaultEventHandler.cs) — где эти события создаются.
