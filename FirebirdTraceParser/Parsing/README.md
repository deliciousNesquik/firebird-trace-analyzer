# Разбор трейса: `Parsing`

Ядро проекта **`FirebirdTraceParser`** — превращает текстовый лог трассировки Firebird в поток
**типизированных событий** (`EventBase` из `Models/Events`). Раздел не зависит от UI: его результат
потребляют приложение (рабочий набор, хранилище, отчёты) и тесты.

Пайплайн собран из четырёх подразделов, каждый со своим README:

| Подраздел | Роль в пайплайне | Документация |
|---|---|---|
| [`Engine`](Engine/README.md) | Верхний уровень: читает файл/поток, режет на блоки по заголовку, собирает результат | `Engine/README.md` |
| [`Rules`](Rules/README.md) | Загрузка/валидация/компиляция regex-правил из внешнего `rules.json` | `Rules/README.md` |
| [`Handlers`](Handlers/README.md) | Превращение одного блока в конкретное типизированное событие | `Handlers/README.md` |
| [`Utils`](Utils/README.md) | Разбор таблицы статистики доступа + хелперы чтения regex-групп | `Utils/README.md` |

---

## Сквозной поток данных

```
rules.json ──(Rules: JsonRuleLoader)──▶ IReadOnlyDictionary<string, Regex>
                                                     │
файл / поток ──▶ Engine: TraceLogParser              │  (правила по имени)
                    │  построчно, КА по block_header │
                    ▼                                ▼
             блок = {Header: Match, BodyLines}  ──▶ Handlers: DefaultEventHandler
                                                    │  диспетчеризация по event_type
                                                    │  разбор тела (+ Utils: PerformanceTableParser,
                                                    │  чтение групп через ParsingExtensions)
                                                    ▼
                                             EventBase (или null → SkippedBlocks)
                                                    │
                              ┌─────────────────────┴─────────────────────┐
                     ParseFile/Async → ParsingResult          ParseStreamAsync → IAsyncEnumerable (батчи)
```

Сквозь весь разбор одного файла проходит единый **`ParsingContext`** (`Infrastructure/Caching`) —
интернирование повторяющихся строк и переиспользование сессий/подключений, что резко снижает
аллокации и размер хранилища.

---

## Кто с кем связан

1. **`Rules`** грузит `rules.json` один раз и отдаёт словарь `имя → Regex` (кэш, валидация схемы,
   проверка обязательных групп и `sample`).
2. **`Engine`** берёт правило `block_header`, режет поток на блоки и на каждый блок зовёт обработчик,
   прокидывая туда весь словарь правил и `ParsingContext`.
3. **`Handlers`** по `event_type` выбирает разбор, читает нужные правила по имени, наполняет событие;
   группы совпадений читает хелперами из **`Utils`**, а таблицу статистики доступа — парсером оттуда же.
4. Результат — `ParsingResult<EventBase>` (весь файл) или поток событий батчами (большие файлы).

Регистрация в DI — `Infrastructure/DependencyInjection/ServiceCollectionExtensions`:
`IRuleLoader`/словарь правил/`IEventHandler` — singleton, `ITraceLogParser` — transient.

---

## Точки расширения

- **Новое правило regex** — добавить в `rules.json` (без пересборки), см. [`Rules`](Rules/README.md).
- **Новый тип события / изменение разбора тела** — [`Handlers`](Handlers/README.md).
- **Новый способ запуска разбора / опции** — [`Engine`](Engine/README.md) (`ITraceLogParser`,
  `ParseOptions`).

---

## Связанные разделы вне `Parsing`

- `Models/Events`, `Models/ValueObjects`, `Models/Enums`, [`Models/Results`](../Models/Results/README.md) —
  формы событий, значений и результата разбора.
- `Infrastructure/Caching` — `ParsingContext`.
- [`Exceptions`](../Exceptions/README.md) — `RuleValidationException`, `SchemaVersionException`.
