# FirebirdTraceParser

Автономная **.NET-библиотека разбора логов трассировки Firebird**. Принимает на вход текстовый
trace-лог (файл или поток) и отдаёт **строго типизированные события** — с подключениями,
транзакциями, SQL, параметрами, метриками производительности и таблицами статистики доступа.

Это ядро приложения *Firebird Trace Analyzer*, но библиотека **самодостаточна и не зависит от UI**
(нет ссылок на Avalonia). Её результат одинаково пригоден для десктоп-приложения, консольной
утилиты, сервиса или тестов.

> Этот файл — «карта» и руководство: **что это, как подключить и как подстроить под себя**.
> Пошаговое устройство каждого узла вынесено в README соответствующих директорий (ссылки в §4).

---

## 1. Чем полезна

- **Типизированный результат.** Не «мешок строк», а иерархия событий (`EventBase` и наследники) с
  вложенными value-объектами — сразу пригодно для фильтров, отчётов, хранилища.
- **Правила разбора — снаружи кода.** Все регулярные выражения лежат в редактируемом `rules.json`;
  под нестандартный формат трейса можно подстроиться **без пересборки**.
- **Потоковость.** Большие файлы разбираются потоково (`IAsyncEnumerable`, батчи) — память не растёт
  с размером файла; есть прогресс и отмена.
- **Устойчивость.** Один битый блок не роняет разбор всего файла — он уходит в предупреждения
  (`SkippedBlocks`), а не в исключение.
- **Экономность.** Агрессивное интернирование повторяющихся строк/объектов, span-чтение regex-групп
  без аллокаций, скомпилированные regex с тайм-аутом (защита от ReDoS).
- **DI-first.** Подключается одной строкой `services.AddFirebirdTraceParser(...)`.

---

## 2. Технологии

| Область | Пакет / платформа |
|---|---|
| Платформа | `net10.0`, `LangVersion=preview`, nullable + implicit usings, `TreatWarningsAsErrors` |
| DI | `Microsoft.Extensions.DependencyInjection` |
| Кэш | `Microsoft.Extensions.Caching.Memory` (кэш скомпилированных правил) |
| Логирование | `NLog` + `NLog.Extensions.Logging` |
| JSON | `System.Text.Json` (чтение `rules.json`) |

---

## 3. Быстрый старт

### Подключение (DI)

```csharp
using FirebirdTraceParser.Infrastructure.DependencyInjection;

services.AddFirebirdTraceParser(
    rulesPath: "путь/к/rules.json",          // обязателен: откуда грузить правила
    nlogConfigPath: "путь/к/nlog.config");    // опционально

// с кастомными опциями разбора:
services.AddFirebirdTraceParser(
    rulesPath: "путь/к/rules.json",
    configureOptions: o =>
    {
        o.ValidationMode = ValidationMode.Relaxed;
        o.ParsePerformanceTables = false;
    });
```

Метод регистрирует: `ILogger`, кэш, загрузчик правил (`IRuleLoader`), сам словарь правил,
обработчик событий (`IEventHandler`) и парсер (`ITraceLogParser`, transient — можно гонять
несколько файлов параллельно).

### Разбор

```csharp
var parser = provider.GetRequiredService<ITraceLogParser>();

// 1) весь файл разом → ParsingResult (события + предупреждения)
ParsingResult<EventBase> result = parser.ParseFile("trace.log");
Console.WriteLine($"{result.Events.Count} событий, пропущено {result.SkippedBlocks}");
if (result.HasErrors) { /* строгий режим поймал сбойные блоки */ }

// 2) асинхронно, с прогрессом и отменой
var progress = new Progress<double>(p => Console.WriteLine($"{p:P0}"));
var res2 = await parser.ParseFileAsync("trace.log", progress, ct);

// 3) потоково для больших файлов — события приходят батчами
await foreach (var evt in parser.ParseStreamAsync(stream, progress, ct))
    Handle(evt);
```

> Реальный пример подключения — `FirebirdTraceAnalyzer/Program.cs` (`AddFirebirdTraceParser` +
> `RulesConfiguration.EnsureRulesFile()`); потоковый разбор — `MainWindowViewModel`.

---

## 4. Архитектура и навигация по документации

Пайплайн: **правила → нарезка на блоки → блок в событие → доменная модель**. У каждой директории
свой подробный README.

### Разбор (`Parsing`) — [обзор пайплайна](Parsing/README.md)

| Директория | Роль | Док |
|---|---|---|
| `Parsing/Engine` | Чтение файла/потока, нарезка на блоки, сбор результата, опции и режим валидации | [README](Parsing/Engine/README.md) |
| `Parsing/Rules` | Загрузка/валидация/компиляция regex-правил из `rules.json` | [README](Parsing/Rules/README.md) |
| `Parsing/Handlers` | Превращение блока в конкретное типизированное событие | [README](Parsing/Handlers/README.md) |
| `Parsing/Utils` | Разбор таблицы статистики доступа + span-хелперы чтения regex-групп | [README](Parsing/Utils/README.md) |

### Доменная модель (`Models`) — [обзор](Models/README.md)

| Директория | Роль | Док |
|---|---|---|
| `Models/Events` | Иерархия событий (`EventBase` и наследники) | [README](Models/Events/README.md) |
| `Models/ValueObjects` | Вложенные данные событий (подключение, транзакция, метрики, …) | [README](Models/ValueObjects/README.md) |
| `Models/Enums` | `EventType` — словарь типов событий | [README](Models/Enums/README.md) |
| `Models/Results` | `ParsingResult`, `ParsingWarning`, `WarningSeverity` | [README](Models/Results/README.md) |

### Инфраструктура и вспомогательное

| Директория | Роль | Док |
|---|---|---|
| `Infrastructure/Caching` | `ParsingContext` — интернирование/дедуп на время разбора | [README](Infrastructure/Caching/README.md) |
| `Infrastructure/DependencyInjection` | `AddFirebirdTraceParser` — регистрация всех служб | *(см. §3)* |
| `Attributes` | `[SortableField]`/`[FilterableField]` — декларативные метки полей для UI | [README](Attributes/README.md) |
| `Enums` | `FilterType` — тип контрола фильтра для поля | [README](Enums/README.md) |
| `Exceptions` | `FirebirdParseException` и наследники (ошибки правил) | [README](Exceptions/README.md) |

---

## 5. Как подстроить под себя

- **Свой формат трейса / новые поля** — правьте `rules.json` (regex с именованными группами,
  `requiredGroups`, `sample`), пересборка не нужна. Загрузчик проверит схему и примеры на старте и
  упадёт с понятной ошибкой, если правило кривое. См. [`Parsing/Rules`](Parsing/Rules/README.md).
- **Поведение разбора** — `ParseOptions`: кодировка, `ValidationMode` (`Strict`/`Relaxed`),
  `BatchSize`, `RegexTimeout`, `ParsePerformanceTables`. См. [`Parsing/Engine`](Parsing/Engine/README.md).
- **Своя логика сборки события** — замените регистрацию `IEventHandler` своей реализацией
  (контракт — [`Parsing/Handlers`](Parsing/Handlers/README.md)).
- **Новые поля в фильтрах/сортировках UI** — пометьте свойство модели атрибутами
  `[SortableField]`/`[FilterableField]`; приложение подхватит их рефлексией без правок UI. См.
  [`Attributes`](Attributes/README.md).

---

## 6. Принципы, заложенные в дизайн

- **Fail-fast на конфигурации, fault-tolerant на данных.** Кривые правила роняют загрузку сразу;
  кривые строки трейса — лишь помечаются предупреждениями, разбор продолжается.
- **Ленивая однократная загрузка правил** с кэшем по времени модификации файла (правка `rules.json`
  сама сбрасывает кэш).
- **Горячий путь без лишних аллокаций** — regex в локалях, span-чтение групп, интернирование.
- **Разделение ответственности** — движок не знает форматов событий, обработчик не знает,
  как читается файл; связь только через словарь правил и `ParsingContext`.

---

## 7. Сборка и тесты

```bash
dotnet build FirebirdTrace.sln      # 0 ошибок (TreatWarningsAsErrors)
dotnet test                          # юнит-тесты (FirebirdTraceAnalyzer.Tests)
```
