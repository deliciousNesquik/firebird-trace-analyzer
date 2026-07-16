# Models/Results

Результат разбора файла: что парсер отдаёт наружу помимо самих событий — список успешно разобранных событий **плюс** предупреждения о проблемных блоках.

Возвращается из [`ITraceLogParser.ParseFile` / `ParseFileAsync`](../../Parsing/Engine/ITraceLogParser.cs). Потоковый [`ParseStreamAsync`](../../Parsing/Engine/TraceLogParser.cs) сюда **не** заворачивается — он отдаёт события по одному (`IAsyncEnumerable<EventBase>`) без агрегата предупреждений.

---

## `ParsingResult<T>`

`sealed record`, `T : EventBase` (на практике `ParsingResult<EventBase>`).

| Член | Тип | Что это |
|---|---|---|
| `Events` | `IReadOnlyList<T>` | успешно разобранные события |
| `Warnings` | `IReadOnlyList<ParsingWarning>` | проблемы разбора (см. ниже) |
| `HasErrors` | `bool` (вычисляемое) | есть ли хоть одно предупреждение с `Severity == Error` |
| `SkippedBlocks` | `int` (вычисляемое) | число предупреждений с `Severity >= Warning` (трактуется как «пропущенные блоки») |

`HasErrors`/`SkippedBlocks` считаются на лету по `Warnings` — отдельно ничего хранить не нужно.

---

## `ParsingWarning`

`sealed record` — одна зафиксированная проблема разбора блока.

| Поле | Тип | Назначение |
|---|---|---|
| `Severity` | `WarningSeverity` | важность (Info/Warning/Error) |
| `Message` | `string` | текст проблемы |
| `LineNumber` | `int` | строка начала блока в файле |
| `BlockContent` | `string?` | первые строки тела блока (контекст; заполняется при ошибке) |
| `EventType` | [`EventType?`](../Enums/README.md) | тип события, если удалось определить |

---

## `WarningSeverity`

```
Info = 0   <   Warning = 1   <   Error = 2
```
Порядок важен: сравнения в `ParsingResult` опираются на него (`>= Warning`, `== Error`).

---

## Кто и когда создаёт предупреждения

Единственный источник — [`TraceLogParser.FlushBlock`](../../Parsing/Engine/TraceLogParser.cs) при обработке блока:

| Ситуация | `Severity` | Message | Попадает в `HasErrors`? | В `SkippedBlocks`? |
|---|---|---|---|---|
| Обработчик вернул `null` (в т.ч. **`Unknown event type`**, не найден маппинг, или не хватило обязательных данных) | `Warning` | `"Block skipped: no event produced …"` (+ `BlockContent`) | нет | **да** |
| Исключение при разборе, режим [`ValidationMode.Strict`](../../Parsing/Engine/ValidationMode.cs) | `Error` | `"Failed to parse block: …"` (+ `BlockContent`) | **да** | **да** |
| Исключение при разборе, режим `Relaxed` | `Warning` | то же | нет | **да** |

Семантика режимов (конвенциональная): **Strict** = сбой разбора это ошибка (`HasErrors=true`), **Relaxed** = сбой это предупреждение (разбор продолжается). Любой **пропущенный блок** (событие не создано) фиксируется как `Warning` и виден в `SkippedBlocks` — не теряется тихо. В частности, событие типа без обвязки (например текущий [`EXECUTE_PROCEDURE_RESTART`](../Enums/README.md)) даст `Warning` «Block skipped», а не молчаливый пропуск.

---

## Потребители

- **Парсер** наполняет `Events`/`Warnings` в [`TraceLogParser`](../../Parsing/Engine/TraceLogParser.cs).
- **Приложение** ([`MainWindowViewModel`](../../../FirebirdTraceAnalyzer/ViewModels/MainWindowViewModel.cs)) сейчас использует **только `Events`**; `Warnings`/`HasErrors`/`SkippedBlocks` наружу не выводятся. Это точка роста: диагностику разбора (сколько блоков пропущено, что не распозналось) можно показывать пользователю.

---

## Связанные разделы

- [`Models/Events`](../Events/README.md) — тип `T` в `ParsingResult<T>` (`EventBase`).
- [`Models/Enums`](../Enums/README.md) — `EventType` в `ParsingWarning`; тема «Unknown event type».
- [`Parsing/Engine`](../../Parsing/Engine/TraceLogParser.cs) — где результат собирается; [`ParseOptions`](../../Parsing/Engine/ParseOptions.cs) / [`ValidationMode`](../../Parsing/Engine/ValidationMode.cs) — что влияет на `Severity`.
