# Движок разбора: `Parsing/Engine`

Верхний уровень парсера: читает файл/поток трейса построчно, **нарезает его на блоки событий** по
заголовку (`block_header`) и отдаёт каждый блок обработчику ([`Parsing/Handlers`](../Handlers)).
Здесь же — публичный контракт парсера, опции разбора и режим валидации. Живёт в проекте
**`FirebirdTraceParser`** (без зависимостей от UI).

| Файл | Назначение |
|---|---|
| `ITraceLogParser.cs` | Публичный контракт: три способа разбора (файл, файл-async, поток-async) |
| `TraceLogParser.cs` | Реализация: построчный конечный автомат «блок → событие», сбор результата |
| `ParseOptions.cs` | Опции разбора (кодировка, режим валидации, размер батча, тайм-аут regex, флаг таблиц) |
| `ValidationMode.cs` | `Strict` / `Relaxed` — как трактовать сбой разбора блока |

---

## 1. Контракт `ITraceLogParser`

```csharp
// Синхронно, весь файл в память
ParsingResult<EventBase> ParseFile(string filePath, ParseOptions? options = null);

// Асинхронно, весь файл + прогресс + отмена
Task<ParsingResult<EventBase>> ParseFileAsync(
    string filePath, IProgress<double>? progress = null,
    CancellationToken cancellationToken = default, ParseOptions? options = null);

// Потоково: события отдаются батчами по мере разбора (не держит всё в памяти)
IAsyncEnumerable<EventBase> ParseStreamAsync(
    Stream stream, IProgress<double>? progress = null,
    CancellationToken cancellationToken = default, ParseOptions? options = null);
```

- **`ParseFile` / `ParseFileAsync`** возвращают `ParsingResult<EventBase>` (`Events` + `Warnings`,
  см. [`Models/Results`](../../Models/Results/README.md)) — удобно, когда нужен весь результат и
  сводка предупреждений разом.
- **`ParseStreamAsync`** — для больших файлов: `yield` батчами по `BatchSize`, память постоянна.
  Предупреждения не возвращаются в потоке — они логируются через `ILogger` в конце (это библиотека,
  `Console` недопустим). Именно этот метод использует приложение (`MainWindowViewModel`).
- `options == null` → `ParseOptions.Default`.

---

## 2. Как режется поток (`ProcessLine`)

Построчный конечный автомат с единственным буфером `BlockBuffer`:

1. Строка матчит `block_header` → **начало нового блока**. Если в буфере уже есть блок — он сначала
   «сбрасывается» (`FlushBlock`), затем буфер сбрасывается и в него кладётся новый заголовок +
   номер строки.
2. Иначе, если блок открыт и строка **непустая**, она добавляется в тело блока.
3. **Пустые строки отбрасываются** (в тело не попадают) — тело блока = только значимые строки
   между двумя заголовками.
4. После конца файла/потока последний открытый блок тоже сбрасывается (`FlushBlock`).

Границы блока определяются **только заголовком** — заранее знать длину события не нужно.

---

## 3. Сброс блока в событие (`FlushBlock`)

1. Зовёт `_handler.Handle(header, bodyLines, rules, context)`.
2. **Событие получено** → добавляется в результат.
3. **`null`** (неизвестный тип или не хватило обязательных данных) → блок пропущен, фиксируется
   `ParsingWarning` уровня `Warning` (первые 3 строки тела в `BlockContent`) — чтобы пропуск попал
   в `SkippedBlocks`, а не потерялся тихо.
4. **Исключение** при разборе — ловится (один битый блок не роняет весь файл) и превращается в
   `ParsingWarning`; уровень зависит от режима (§4). Плюс `Warn` в лог.

Единый `ParsingContext` создаётся **на файл/поток** и прокидывается во все блоки — межблочный кэш
интернирования и переиспользование сессий/подключений работает на протяжении всего разбора.

---

## 4. Опции и валидация

### `ParseOptions` (record, значения по умолчанию)

| Опция | Умолчание | Смысл |
|---|---|---|
| `Encoding` | `UTF-8` | Кодировка файла/потока |
| `ValidationMode` | `Strict` | Трактовка сбоя разбора блока (см. ниже) |
| `BatchSize` | `256` | Размер батча для `ParseStreamAsync` |
| `RegexTimeout` | `1 сек` | Тайм-аут regex-операций |
| `ParsePerformanceTables` | `true` | Разбирать ли таблицы статистики доступа (использует обработчик) |

`ParseOptions.Default` — новый экземпляр со значениями по умолчанию.

### `ValidationMode`

- **`Strict`** — сбой разбора блока = `Error` → `ParsingResult.HasErrors == true`.
- **`Relaxed`** — сбой = `Warning`, разбор продолжается, `HasErrors` не поднимается (проблемы видны
  в `SkippedBlocks`).

В обоих режимах сам разбор не прерывается — отличается только уровень записанного предупреждения.

---

## 5. Прогресс и отмена

- **Прогресс** в `ParseFileAsync` — по позиции потока (`stream.Position / fileSize`), раз в 1000
  строк: дёшево и без повторного кодирования строк. В конце — `Report(1.0)`.
- **Отмена** (`CancellationToken`) проверяется на каждой строке (`ThrowIfCancellationRequested`);
  чтение — `ReadLineAsync(ct)`.
- `ParseFileAsync` открывает `FileStream` с `useAsync: true` и буфером 80 КБ.

---

## 6. Кто вызывает и как регистрируется

- **DI** (`Infrastructure/DependencyInjection/ServiceCollectionExtensions`):
  `ITraceLogParser → TraceLogParser` — **Transient** (для параллельного разбора нескольких файлов).
  В конструктор внедряются словарь правил, `IEventHandler` и `ILogger`.
- **Приложение**: `MainWindowViewModel` разбирает файлы через `ParseStreamAsync` (потоково, с
  прогрессом и отменой), складывая события в рабочий набор/хранилище.

---

## 7. Связанные разделы

- [`Parsing/Rules`](../Rules/README.md) — правило `block_header`, по которому режется поток, и все
  остальные правила, передаваемые в обработчик.
- [`Parsing/Handlers`](../Handlers/README.md) — превращение блока в типизированное событие.
- [`Models/Results`](../../Models/Results/README.md) — `ParsingResult`, `ParsingWarning`,
  `WarningSeverity` (`HasErrors`, `SkippedBlocks`).
- `Infrastructure/Caching` — `ParsingContext` (единый на разбор).
