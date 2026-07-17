# Обработчики событий: `Parsing/Handlers`

Превращают **распознанный блок трейса** (заголовок-`Match` + строки тела) в **типизированное
событие** (`EventBase`-наследник из `Models/Events`). Это «мясо» разбора: движок
([`Parsing/Engine`](../Engine)) только нарезает лог на блоки и находит заголовок правилом
`block_header`, а _что это за событие и какие у него поля_ — решает обработчик.

| Файл | Назначение |
|---|---|
| `IEventHandler.cs` | Контракт: `EventBase? Handle(Match blockHeader, IReadOnlyList<string> bodyLines, IReadOnlyDictionary<string,Regex> rules, ParsingContext context)` |
| `DefaultEventHandler.cs` | Единственная реализация: диспетчеризация по типу события + разбор тела всех 14 типов |

---

## 1. Контракт `IEventHandler`

```csharp
EventBase? Handle(
    Match blockHeader,                              // совпадение правила block_header
    IReadOnlyList<string> bodyLines,                // строки тела блока (без заголовка)
    IReadOnlyDictionary<string, Regex> rules,       // скомпилированные правила (см. Parsing/Rules)
    ParsingContext context);                        // кэш интернирования + сессий/подключений
```

- Возвращает готовое событие или **`null`** — сигнал «блок не удалось разобрать как валидное
  событие» (движок такой блок пропускает и логирует `Warn`).
- Обработчик не бросает на неполных данных: недостающие поля дают `null`, а не исключение — один
  битый блок не должен ронять разбор всего файла.

---

## 2. Диспетчеризация (`DefaultEventHandler.Handle`)

1. Читает группу `event_type` из заголовка.
2. **Особый случай** `ERROR AT <компонент>` — уходит в `HandleError` (формат заголовка отличается
   от остальных: тип не из фиксированного списка, а с именем компонента).
3. Иначе ищет тип в **`EventTypeMapping`** (строка Firebird → `EventType`, `OrdinalIgnoreCase`).
   Нет в маппинге → `Warn "Unknown event type"` + `null`.
4. `switch` по `EventType` вызывает соответствующий `Handle*`. Если тот вернул `null` —
   `Warn "Handler returned null"`.

Поддерживаемые типы (14 + `ERROR AT`):

| Семейство | Типы событий |
|---|---|
| Трассировка | `TRACE_INIT`, `TRACE_FINI` |
| Подключение | `ATTACH_DATABASE`, `DETACH_DATABASE` |
| Statement | `EXECUTE_STATEMENT_{START,RESTART,FINISH}`, `FAILED EXECUTE_STATEMENT_FINISH` |
| Procedure | `EXECUTE_PROCEDURE_{START,FINISH}`, `FAILED EXECUTE_PROCEDURE_FINISH` |
| Trigger | `EXECUTE_TRIGGER_{START,FINISH}`, `FAILED EXECUTE_TRIGGER_FINISH` |
| Ошибка | `ERROR AT <компонент>` |

---

## 3. Устройство обработчика

Каждый `Handle*` следует одному шаблону:

1. Разобрать тело нужным «сложным парсером» (`ParseStatementData` / `ParseProcedureData` /
   `ParseTriggerData`) или узкими хелперами (`ParseSessionInfo`, `ParseAttachmentInfo`).
2. **Проверить обязательные части** — если нет `Attachment` (а для finish/failed ещё и
   `Transaction`, для процедур — `ProcedureName`) → вернуть `null`.
3. Достать общие метаданные заголовка (`ParseEventMetadata`: `timestamp`, `trace_id`, `hex_trace_id`).
4. Собрать и вернуть конкретный `EventBase`-наследник.

### Общие узлы

- **`ParseEventMetadata`** — `ts` → `DateTime` (инвариантная культура; при неудаче `default`),
  `trace_id` → int, `hex_trace_id` интернируется.
- **`CreateDefaultPerformance`** — нулевой `PerformanceInfo` как фолбэк, если метрик в теле нет
  (поле события всегда непустое).

### Хелперы разбора тела

| Хелпер | Что достаёт |
|---|---|
| `ParseSessionInfo` | Данные сессии трассировки (`session`) |
| `ParseAttachmentInfo` | Подключение: БД, пользователь, роль, charset, протокол, адрес/порт, процесс/PID |
| `ParseTransactionInfo` | Транзакция (id + параметры) |
| `ParseErrorChain` | Цепочка строк ошибки (код + текст) |
| `ParseSqlParameter` | Один входной SQL-параметр (имя, тип, значение / `<NULL>`) |
| `ParsePerformance` | Метрики строки `… ms, read(s), write(s), fetch(es), mark(s)` |

### Сложные парсеры (`ParseStatementData` / `…Procedure…` / `…Trigger…`)

Идут по строкам тела **одним проходом** и наполняют промежуточный record
(`StatementData` / `ProcedureData` / `TriggerData`):

- Regex-правила берутся **в локальные переменные** до цикла — убираем словарные лукапы из горячего пути.
- Транзакция/statement-id/счётчик рестартов — по своим правилам.
- **SQL-блок** начинается после разделителя `-----` и собирается до следующего значимого маркера.
- **Метрики** (`performance`) и, для finish-событий, **таблица статистики доступа** к таблицам —
  через [`PerformanceTableParser`](../Utils/README.md#1-performancetableparser).
- Чтение групп совпадений — через [`ParsingExtensions`](../Utils/README.md#7-parsingextensions)
  (span-перегрузки без аллокаций).

---

## 4. Связь с `ParseOptions`

`DefaultEventHandler(ILogger, ParseOptions?)`. Из опций напрямую используется флаг
**`ParsePerformanceTables`**: даже если таблица статистики разобрана, в событие
(`PerformanceTable`) она кладётся только при включённом флаге — иначе `null`. Остальные опции
(`Encoding`, `BatchSize`, `RegexTimeout`, `ValidationMode`) относятся к движку, не к обработчику.

---

## 5. `ParsingContext` — дедупликация

Обработчик агрессивно **интернирует** повторяющиеся строки (пути к БД, имена таблиц/процедур,
пользователей, hex-id) через `context.Intern(...)` и переиспользует объекты сессий/подключений
(`InternSession`, `TryGetAttachment`) — в трейсе эти значения повторяются массово между событиями,
и дедуп резко снижает аллокации и размер хранилища. Детали кэша — `Infrastructure/Caching`.

---

## 6. Кто вызывает и как регистрируется

- **`TraceLogParser`** (`Parsing/Engine`) на каждый распознанный блок зовёт
  `_handler.Handle(block.Header!, block.BodyLines, _rules, context)` и собирает поток событий.
- **DI** (`Infrastructure/DependencyInjection/ServiceCollectionExtensions`):
  `IEventHandler → DefaultEventHandler` (singleton); словарь правил и `ILogger` внедряются туда же.

---

## 7. Связанные разделы

- [`Parsing/Rules`](../Rules/README.md) — правила regex, к которым обращаются обработчики по имени.
- [`Parsing/Utils`](../Utils/README.md) — `PerformanceTableParser` и `ParsingExtensions`.
- `Models/Events`, `Models/ValueObjects`, `Models/Enums` — формы результата (`EventBase`-наследники,
  `AttachmentInfo`/`TransactionInfo`/`PerformanceInfo`/…, `EventType`).
- `Infrastructure/Caching` — `ParsingContext` (интернирование и кэш сессий/подключений).
