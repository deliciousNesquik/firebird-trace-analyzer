# Правила разбора: `Parsing/Rules`

Загрузка, валидация и компиляция **regex-правил** парсера из внешнего JSON-файла
(`rules.json`). Живёт в проекте **`FirebirdTraceParser`** (без зависимостей от UI). Смысл раздела —
вынести все регулярные выражения из кода в редактируемый конфиг: пользователь/поддержка может
править шаблоны под нестандартный формат трейса, не пересобирая приложение.

| Файл | Назначение |
|---|---|
| `IRuleLoader.cs` | Контракт загрузчика: `IReadOnlyDictionary<string, Regex> LoadRules(string configPath)` |
| `JsonRuleLoader.cs` | Реализация: чтение JSON, проверка схемы, десериализация, компиляция + валидация, кэш |
| `RuleConfiguration.cs` | DTO корня файла: `SchemaVersion` + `Rules` (имя → определение) |
| `RuleDefinition.cs` | DTO одного правила: `Pattern`, `Flags`, `Description`, `RequiredGroups`, `Sample` |

Итог работы раздела — `IReadOnlyDictionary<string, Regex>` (имя правила → скомпилированный `Regex`),
который потом получают `TraceLogParser` и `DefaultEventHandler` через DI.

---

## 1. Формат правила (`RuleDefinition`)

Каждое правило в `rules.json` — объект под своим именем-ключом:

```json
"performance": {
  "pattern": "^\\s*(?<execute_ms>\\d+)\\s+ms,\\s*(?:(?<read>\\d+)\\s+read\\(s\\),\\s*)?...",
  "flags": ["IgnorePatternWhitespace"],
  "description": "Регулярное выражение для извлечения метрик выполнения.",
  "requiredGroups": ["execute_ms", "read", "write", "fetch", "mark"],
  "sample": "377 ms, 6 read(s), 469 write(s), 1446 fetch(es), 1440 mark(s)"
}
```

| Поле | Обяз. | Назначение |
|---|---|---|
| `pattern` | да | Шаблон регулярного выражения (именованные группы `(?<name>…)`) |
| `flags` | нет | Массив имён `RegexOptions` (см. §3). Отсутствует/пуст → дефолт |
| `description` | нет | Человекочитаемое описание (в коде не используется, только для конфига) |
| `requiredGroups` | нет | Именованные группы, которые **обязаны** присутствовать в скомпилированном regex |
| `sample` | нет | Строка-пример; если задана, regex **обязан** её матчить, иначе загрузка падает |

`RuleConfiguration` — корень файла: `schemaVersion` (int) + `rules` (словарь). Оба DTO `internal`
(детали загрузчика), десериализуются с `PropertyNameCaseInsensitive`.

---

## 2. Алгоритм `JsonRuleLoader.LoadRules`

1. **Проверка файла.** Нет файла → `Fatal` + `RuleValidationException`.
2. **Кэш.** Ключ = `Rules_{путь}_{LastWriteTimeUtc.Ticks}` в `IMemoryCache`, sliding-expiration **1 час**.
   Правка `rules.json` меняет время записи → ключ другой → правила перечитываются автоматически.
3. **Версия схемы.** Читается `schemaVersion`; при `!= 1` (`SupportedSchemaVersion`) →
   `SchemaVersionException`. Это защищает от загрузки конфига несовместимого формата.
4. **Десериализация** в `RuleConfiguration`.
5. **Компиляция + валидация** каждого правила (`CompileAndValidate`, см. §4).
6. Лог `Loaded N rules successfully`, возврат словаря `имя → Regex`.

Загрузка **ленивая и однократная** на файл (см. §5) — тяжёлая компиляция regex не повторяется.

---

## 3. Флаги regex (`ParseFlags`)

Поддерживаемые имена флагов (маппинг на `RegexOptions`):

| Имя в JSON | `RegexOptions` |
|---|---|
| `IgnoreCase` | `IgnoreCase` |
| `Multiline` | `Multiline` |
| `Singleline` | `Singleline` |
| `IgnorePatternWhitespace` | `IgnorePatternWhitespace` |
| `ExplicitCapture` | `ExplicitCapture` |

- Неизвестное имя флага → `RuleValidationException` («Неизвестный флаг regex»).
- **Дефолт при отсутствии/пустом `flags`** — `IgnorePatternWhitespace`. То есть даже правило с
  `"flags": []` получает режим «пробелы в шаблоне игнорируются» (значимые пробелы нужно
  экранировать `\ ` или классом). Это важно учитывать при написании новых шаблонов.
- К любому набору флагов всегда добавляется **`RegexOptions.Compiled`** (скорость на горячем пути).
- Каждый regex компилируется с **тайм-аутом 1 сек** — защита от катастрофического бэктрекинга
  (ReDoS) на битой строке трейса.

---

## 4. Валидация при компиляции (`CompileAndValidate`)

Для каждого правила по очереди:

1. **Компиляция** `new Regex(pattern, flags | Compiled, timeout: 1s)`.
2. **Обязательные группы.** Множество `requiredGroups` должно быть подмножеством фактических
   **именованных** групп regex (числовые группы игнорируются). Недостающие → `Fatal` +
   `RuleValidationException` со списком. Это ловит опечатку в имени группы до рантайма.
3. **Проверка примера.** Если задан `sample` и regex его **не** матчит → `RuleValidationException`
   (с `SampleData`). Гарантия, что шаблон действительно разбирает заявленный формат.
4. Любое иное исключение при компиляции (битый шаблон и т.п.) заворачивается в
   `RuleValidationException` с именем правила.

Принцип: **fail-fast на старте**. Кривой конфиг роняет загрузку с понятной ошибкой, а не выдаёт
молча пустые/неверные события во время разбора.

---

## 5. Где берётся `rules.json` и как всё связано

- **`RulesConfiguration`** (`FirebirdTraceAnalyzer/Services`) при первом запуске копирует
  поставляемые `rules.json` + `rules.schema.json` из бандла в пользовательскую папку
  `%AppData%/FirebirdTraceAnalyzer` и возвращает путь. Пользователь правит правила там, не трогая
  файлы приложения; при сбое — фолбэк на бандл.
- **DI** (`Infrastructure/DependencyInjection/ServiceCollectionExtensions`):
  - `IRuleLoader → JsonRuleLoader` (singleton);
  - `IReadOnlyDictionary<string, Regex>` регистрируется фабрикой, которая зовёт `LoadRules(rulesPath)`
    (singleton — правила грузятся один раз на приложение).
- **Потребители** результата: `TraceLogParser` и `DefaultEventHandler` (`Parsing/Handlers`) — берут
  готовый словарь и обращаются к правилам по имени (`rules["performance"]`, `rules["block_header"]`
  и т.д.). Чтение групп совпадений идёт через хелперы [ParsingExtensions](../Utils/README.md#7-parsingextensions).
- **`rules.schema.json`** — JSON-Schema (draft-07) для валидации файла в редакторе; в рантайме
  загрузчик её не применяет, но проверяет `schemaVersion` вручную.
- **Исключения** — см. [Exceptions](../../Exceptions/README.md): `RuleValidationException`,
  `SchemaVersionException` (обе наследуют `FirebirdParseException`).

---

## 6. Как добавить/изменить правило

1. Добавить объект в `rules` файла `rules.json` (не забыть `schemaVersion: 1`).
2. Указать `pattern` с **именованными** группами; перечислить их в `requiredGroups`.
3. Приложить `sample` с реальной строкой трейса — это самопроверка шаблона на старте.
4. Помнить про дефолтный `IgnorePatternWhitespace` (§3): значимые пробелы экранировать.
5. В коде обращаться к правилу по имени через словарь и читать группы через `ParsingExtensions`.
6. Проверить сборку/тесты — кривое правило упадёт на загрузке с `RuleValidationException`.
