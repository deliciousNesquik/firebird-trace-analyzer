# Models/ValueObjects

Вложенные данные событий трассировки — «строительные блоки», на которые ссылаются классы из [`Models/Events`](../Events/README.md). Сами по себе это не события, а их части: подключение, транзакция, параметры, метрики производительности, ошибки.

Большинство свойств помечены атрибутами [`[SortableField]`/`[FilterableField]`](../../Attributes/README.md) — и попадают в фильтры/сортировки/отчёты по **вложенному пути** (например `Attachment.User`, `Performance.ExecuteMs`), который резолвит `EventPropertyAccessor` компилируемым геттером.

---

## Обзор

| Объект | Тип | Используется в событиях | Как хранится ([`EventStoreService`](../../../FirebirdTraceAnalyzer/Services/Persistence/EventStoreService.cs)) | Память |
|---|---|---|---|---|
| `AttachmentInfo` | `sealed class` | attach/detach, statement, procedure, trigger, error | дедуп-таблица `attachment` (UNIQUE по sha полей) | **пулится** по `AttachmentId` |
| `TransactionInfo` | `sealed record` | statement, procedure, trigger | плоские колонки `event.txn_*` | новый на событие |
| `TraceSessionInfo` | `sealed record` | TraceInit, TraceFinish | колонка `event.session_id` | **пулится** по `SessionId` |
| `SqlParameters` | `sealed record` | statement, procedure (`Parameters[]`) | дочерняя таблица `sql_parameter` | новый на параметр (строки интернируются) |
| `PerformanceInfo` | `sealed record` | *Finish, Failed*Finish | колонки `event.perf_*` | новый на finish-событие |
| `PerformanceTable` / `PerformanceTableItem` | `sealed record` | *Finish, Failed*Finish (опц.) | дочерняя таблица `perf_table_item` (+ `perf_table_state`) | опц., см. `ParseOptions.ParsePerformanceTables` |
| `ErrorLines` | `sealed record` | ErrorEvent (`Errors[]`) | дочерняя таблица `error_line` | новый на строку ошибки |

---

## Объекты

### `AttachmentInfo` — подключение к БД
`sealed class` (не record: идентичность важна — объект переиспользуется из пула по `AttachmentId`).

Обязательные: `DatabasePath`, `AttachmentId`, `User`, `Role`, `Charset`, `Protocol`, `Address`, `Port`. Опциональные: `ProcessPath?`, `ProcessId?` (путь/PID клиентского процесса). Все — фильтруемые (`StringMultiSelect`, категория «Attachment»); большинство ещё и сортируемые. В UI это самый богатый разрез (кто/откуда/чем подключался).

### `TransactionInfo` — транзакция
`sealed record`, **все поля nullable**: `TransactionId?`, `IsolationLevel?`, `ConsistencyMode?`, `LockMode?`, `AccessMode?` (категория «Transaction»). Строковые значения парсер выставляет литералами (`"READ_COMMITTED"`, `"NOWAIT"`, …), уже интернированными CLR. Создаётся заново на каждое событие с транзакцией (`TransactionId` почти уникален — дедуп бесполезен).

### `TraceSessionInfo` — глобальная trace-сессия
`sealed record`, одно поле `SessionId` (required, категория «Global»). Переиспользуется из пула по `SessionId`.

### `SqlParameters` — один параметр запроса
`sealed record`: `Name`, `Dtype`, `Value` (все required, строки). `ToString()` → `"<имя> (<тип>) = <значение>"`. **Без атрибутов** — это элемент коллекции `Parameters[]`, отдельным полем в фильтры не выносится. Строки интернируются при разборе.

### `PerformanceInfo` — метрики выполнения
`sealed record`, 5 required `int`: `ExecuteMs`, `FetchCount`, `ReadCount`, `WriteCount`, `MarkCount` (категория «Performance», фильтр `NumericRange`). Когда метрик в блоке нет — обработчик подставляет нулевой экземпляр (`CreateDefaultPerformance`).

### `PerformanceTable` / `PerformanceTableItem` — статистика по таблицам
`PerformanceTable` = обёртка над `IReadOnlyList<PerformanceTableItem>? Items` (nullable). `PerformanceTableItem` — `sealed record`: `TableName` + 8 счётчиков (`Natural/Index/Update/Insert/Delete/Backout/Purge/Expunge`Count). **Без атрибутов** (табличная детализация, не поле фильтра). Наполняется только при `ParseOptions.ParsePerformanceTables = true`; в сторе есть флаг `perf_table_state`, различающий «таблицы нет / есть, но Items=null / есть Items».

### `ErrorLines` — одна ошибка в цепочке
`sealed record`: `ErrorCode` (int), `Message` (string, дефолт `""`). `ToString()` → `"<код>: <сообщение>"`. Собираются в `ErrorEvent.Errors[]`; фильтр «Codes» объявлен на самом списке в [`ErrorEvent`](../Events/ErrorEvent.cs), а не на полях `ErrorLines`.

---

## Как поля попадают в UI

Атрибут на свойстве value-объекта → поле доступно в фильтрах/сортировках/дизайнере отчётов **через вложенный путь** от события:

```
StatementFinishEvent.Attachment.User        →  фильтр "User"        (категория Attachment)
StatementFinishEvent.Performance.ExecuteMs   →  фильтр "Execution Time (ms)" (Performance, NumericRange)
ProcedureStartEvent.Transaction.IsolationLevel → фильтр "Isolation Level" (Transaction)
```

Путь резолвит [`EventPropertyAccessor`](../../../FirebirdTraceAnalyzer/Services/EventProperties/EventPropertyAccessor.cs) (компилируемый геттер, кэш). Тип контрола задаёт [`FilterType`](../../Enums/README.md); механика атрибутов — в [`Attributes`](../../Attributes/README.md).

Объекты **без атрибутов** (`SqlParameters`, `PerformanceTableItem`, `ErrorLines`) в фильтры полями не выносятся — это коллекции-детализация, показываются в карточке события целиком.

---

## Заметки

- **`class` vs `record`.** Только `AttachmentInfo` — `class` (переиспользуется из пула по id, где нужна ссылочная идентичность). Остальные — `record` (значимое равенство; удобно для сравнения/дедупа и иммутабельности).
- **Пулинг/интернирование** (детали — [`Infrastructure/Caching`](../../Infrastructure/Caching)): `AttachmentInfo` и `TraceSessionInfo` переиспользуются по id (один объект на уникальный attachment/сессию), строковые поля интернируются per-parse; `TransactionInfo`/`SqlParameters`/`PerformanceInfo` создаются на событие.
- **Хранилище**: словари-дедупы (`attachment`) и дочерние таблицы (`sql_parameter`, `perf_table_item`, `error_line`) против плоских колонок (`txn_*`, `perf_*`, `session_id`) — раскладку задаёт [`EventStoreService`](../../../FirebirdTraceAnalyzer/Services/Persistence/EventStoreService.cs) (single-table inheritance для событий + дедуп-словари).

---

## Связанные разделы

- [`Models/Events`](../Events/README.md) — какие события какие value-объекты содержат.
- [`Models/Enums`](../Enums/README.md) — `EventType`.
- [`Attributes`](../../Attributes/README.md) + [`Enums/FilterType`](../../Enums/README.md) — как поля попадают в фильтры/сортировки.
- [`Parsing/Handlers/DefaultEventHandler.cs`](../../Parsing/Handlers/DefaultEventHandler.cs) — где эти объекты создаются/наполняются.
