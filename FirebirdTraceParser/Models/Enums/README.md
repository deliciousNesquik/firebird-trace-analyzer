# Models/Enums

Перечисления доменной модели парсера. Сейчас здесь один файл — [`EventType.cs`](EventType.cs), центральный «словарь» типов событий трассировки Firebird.

---

## `EventType`

`public enum EventType` — тип одного разобранного события трейса. Значение хранится в базовом классе события [`EventBase.EventType`](../Events/EventBase.cs) и определяет, какой конкретный класс-наследник создан и как событие интерпретируется дальше по конвейеру.

Каждое значение помечено атрибутом `[Description("…")]`, где строка — **сырой токен Firebird** из заголовка блока трейса (`event_type`). Пример строки лога:

```
2024-05-01T10:15:42.1234 (ATTACH_DATABASE) …
                          └──── event_type ────┘
```

### Полная таблица значений

| `EventType` | `[Description]` (токен трейса) | Класс события ([`Models/Events`](../Events)) | Обработчик ([`DefaultEventHandler`](../../Parsing/Handlers/DefaultEventHandler.cs)) |
|---|---|---|---|
| `TraceInit` | `TRACE_INIT` | `TraceInitEvent` | `HandleTraceInit` |
| `TraceFinish` | `TRACE_FINI` | `TraceFinishEvent` | `HandleTraceFinish` |
| `AttachDatabase` | `ATTACH_DATABASE` | `AttachDatabaseEvent` | `HandleAttach` |
| `DetachDatabase` | `DETACH_DATABASE` | `DetachDatabaseEvent` | `HandleDetach` |
| `ExecuteStatementStart` | `EXECUTE_STATEMENT_START` | `StatementStartEvent` | `HandleStatementStart` |
| `ExecuteStatementRestart` | `EXECUTE_STATEMENT_RESTART` | `StatementRestartEvent` | `HandleStatementRestart` |
| `ExecuteStatementFinish` | `EXECUTE_STATEMENT_FINISH` | `StatementFinishEvent` | `HandleStatementFinish` |
| `ExecuteProcedureStart` | `EXECUTE_PROCEDURE_START` | `ProcedureStartEvent` | `HandleProcedureStart` |
| `ExecuteProcedureRestart` ⚠️ | `EXECUTE_PROCEDURE_RESTART` | — | — (см. ниже) |
| `ExecuteProcedureFinish` | `EXECUTE_PROCEDURE_FINISH` | `ProcedureFinishEvent` | `HandleProcedureFinish` |
| `ExecuteTriggerStart` | `EXECUTE_TRIGGER_START` | `TriggerStartEvent` | `HandleTriggerStart` |
| `ExecuteTriggerFinish` | `EXECUTE_TRIGGER_FINISH` | `TriggerFinishEvent` | `HandleTriggerFinish` |
| `FailedExecuteStatementFinish` | `FAILED EXECUTE_STATEMENT_FINISH` | `FailedStatementFinishEvent` | `HandleFailedStatementFinish` |
| `FailedExecuteProcedureFinish` | `FAILED EXECUTE_PROCEDURE_FINISH` | `FailedProcedureFinishEvent` | `HandleFailedProcedureFinish` |
| `FailedExecuteTriggerFinish` | `FAILED EXECUTE_TRIGGER_FINISH` | `FailedTriggerFinishEvent` | `HandleFailedTriggerFinish` |
| `Error` ⚠️ | `ERROR` | `ErrorEvent` | `HandleError` (по префиксу, см. ниже) |

---

## Как токен трейса превращается в `EventType`

Диспетчеризация — в [`DefaultEventHandler.Handle`](../../Parsing/Handlers/DefaultEventHandler.cs). Важно: **`[Description]` для диспетчеризации не читается через рефлексию** — используется явная карта `EventTypeMapping` (`Dictionary<string, EventType>`), а `Error` распознаётся по префиксу строки.

```
event_type (строка из заголовка блока)
        │
        ├─ StartsWith("ERROR AT ")  ──────────────► EventType.Error ─► HandleError
        │        (напр. "ERROR AT JProvider::attach")
        │
        ├─ EventTypeMapping.TryGetValue(str) ──────► EventType.X ─► switch → HandleX → EventBase
        │
        └─ не найдено ─► Logger.Warn("Unknown event type") ─► null (блок пропущен)
```

### Два особых случая

- **`Error`** — в трейсе токен выглядит как `ERROR AT <компонент>` (например `ERROR AT JProvider::attach`), поэтому его нельзя сопоставить фиксированной строкой. Он ловится проверкой `StartsWith("ERROR AT ")` **до** карты; сам компонент вырезается в `HandleError`. Поэтому `Error` **отсутствует** в `EventTypeMapping`.
- **`ExecuteProcedureRestart`** — значение объявлено в enum, но **не имеет** записи в `EventTypeMapping` и ветки в `switch`, то есть парсером сейчас не производится (Firebird в наблюдаемых трейсах такой токен для процедур не эмитит). Значение зарезервировано ради полноты множества; при появлении реальных данных нужно добавить и маппинг, и обработчик, и класс события.

---

## Потребители `EventType`

1. **Парсер (диспетчеризация)** — [`DefaultEventHandler`](../../Parsing/Handlers/DefaultEventHandler.cs): строка → `EventType` → выбор `HandleX` → конкретный [`EventBase`](../Events/EventBase.cs)-наследник.
2. **UI-фильтры** — [`FilteringService`](../../../FirebirdTraceAnalyzer/Services/Filtering/FilteringService.cs): для отображения значений фильтра «тип события» подпись берётся из `[Description]` через `GetEnumDisplayName` (рефлексия `DescriptionAttribute`). То есть в интерфейсе пользователь видит именно токены Firebird (`TRACE_INIT`, `EXECUTE_STATEMENT_FINISH`, …).
3. **Хранилище** — [`EventStoreService`](../../../FirebirdTraceAnalyzer/Services/Persistence/EventStoreService.cs): колонка `event.event_type INTEGER` хранит **`(int)EventType`** (порядковый номер значения). Индекса по типу нет намеренно — фильтрация типов выполняется в памяти UI.

---

## Контракты и предупреждения при изменении

- **`[Description]` = точный токен Firebird.** Менять только при изменении формата трейса; строка одновременно участвует в UI-подписи фильтра.
- **Порядок значений — стабильный контракт хранилища.** В `event.event_type` пишется `(int)EventType` (ordinal). Перестановка/вставка значений в середину **переинтерпретирует** уже записанные события в существующей БД. Добавлять новые значения безопаснее **в конец**. (Полный сброс стора происходит только при смене `SchemaVersion`, не при изменении enum.)
- **Добавление нового типа события** требует согласованно обновить: (1) значение в `EventType`, (2) запись в `EventTypeMapping`, (3) ветку `switch` в `Handle`, (4) метод-обработчик `HandleX`, (5) класс события в [`Models/Events`](../Events). Пропуск (2)/(3) приведёт к `Unknown event type`/`null` и молчаливому пропуску блока.

---

## Связанные разделы

- [`Models/Events`](../Events) — классы событий, создаваемые по каждому `EventType`.
- [`Models/ValueObjects`](../ValueObjects) — вложенные данные событий (Attachment/Transaction/Performance/…).
- [`Parsing/Handlers/DefaultEventHandler.cs`](../../Parsing/Handlers/DefaultEventHandler.cs) — маппинг и обработчики.
