# Исключения: `FirebirdParseException` и наследники

Собственные типы исключений библиотеки парсера. Один базовый абстрактный класс + два конкретных
наследника. Все они относятся к **загрузке и валидации правил парсинга** (`rules.json`), а не к
разбору событий как таковому — см. §4 про важный нюанс имени.

Живёт в проекте **`FirebirdTraceParser`** (без зависимостей от UI).

---

## 1. Иерархия

```
Exception
  └─ FirebirdParseException            (abstract) — единый базовый тип библиотеки
       ├─ RuleValidationException      — правило некорректно / не проходит валидацию
       └─ SchemaVersionException       — версия схемы rules.json не поддерживается
```

Базовый `FirebirdParseException` даёт **один тип для перехвата** всех «библиотечных» ошибок
(`catch (FirebirdParseException)`), хотя сейчас так нигде не ловят — см. §5.

---

## 2. Справочник классов

| Класс | Тип | Поля | Конструктор | Когда |
|---|---|---|---|---|
| `FirebirdParseException` | `abstract` | — | `(message)`, `(message, inner)` — оба `protected` | базовый; напрямую не бросается |
| `RuleValidationException` | `sealed` | `RuleName?`, `SampleData?` | `(message, ruleName = null)` | правило не прошло валидацию/компиляцию |
| `SchemaVersionException` | `sealed` | `ExpectedVersion`, `ActualVersion` | `(expected, actual)` — сообщение формируется само | `schemaVersion` в файле ≠ поддерживаемой |

Поля `RuleName`/`SampleData`/`ExpectedVersion`/`ActualVersion` — **диагностические**: заполняются при
броске, но ни один обработчик их сейчас не читает (см. §5). Полезны в логах/отладке и на будущее.

---

## 3. Где бросаются (все — в `Parsing/Rules/JsonRuleLoader.cs`)

Оба конкретных исключения бросаются **только** загрузчиком правил `JsonRuleLoader`:

| Исключение | Условие | Метод |
|---|---|---|
| `RuleValidationException` | файл `rules.json` не найден | `LoadRules` |
| `SchemaVersionException` | `schemaVersion != SupportedSchemaVersion` (сейчас `1`) | `LoadRules` |
| `RuleValidationException` | в regex нет обязательных групп (`RequiredGroups`) | `CompileAndValidate` |
| `RuleValidationException` | regex не совпал со своим `Sample` (проставляет `SampleData`) | `CompileAndValidate` |
| `RuleValidationException` | ошибка компиляции regex (обёртка любого не-`RuleValidationException`) | `CompileAndValidate` (`catch when`) |
| `RuleValidationException` | неизвестный флаг regex во `Flags` | `ParseFlags` |

Сам разбор строк трейса (`TraceLogParser`) этих исключений **не бросает** — несовпавшие строки он
пропускает/логирует, а не роняет процесс.

---

## 4. Нюанс имени

Несмотря на «Parse» в названии, эти исключения — про **конфигурацию правил**, а не про парсинг
событий. Правила (`rules.json`) — это regex-описания, по которым парсер узнаёт строки трейса;
`FirebirdParseException` сигналит, что **сами правила плохи или несовместимы**, ещё до разбора данных.

---

## 5. Как всплывают (поток и обработка)

```
AddFirebirdTraceParser(rulesPath)                 Infrastructure/DependencyInjection
    └─ регистрирует ЛЕНИВЫЙ singleton
       IReadOnlyDictionary<string,Regex> = () => JsonRuleLoader.LoadRules(rulesPath)
                          │  (реальная загрузка — при первом резолве)
                          ▼
Program.ValidateParserConfiguration (старт приложения)
    provider.GetRequiredService<IReadOnlyDictionary<string,Regex>>()   ← тут срабатывает LoadRules
        │
        ├─ успех → правила загружены, приложение стартует
        └─ FirebirdParseException (или иное) → catch (Exception) →
             logger.Fatal("Failed to load parser rules…") → throw → приложение НЕ стартует
```

Здесь `rulesPath` = `RulesConfiguration.EnsureRulesFile()` — путь к **пользовательской** копии
`%AppData%/FirebirdTraceAnalyzer/rules.json` (при первом запуске сидируется из поставляемого
`<каталог приложения>/Configuration/rules.json`). То есть валидируется именно пользовательский файл.

Практический смысл: `FirebirdParseException` — это **фатальная ошибка старта/конфигурации**.
Загрузка правил ленивая и кэшируется (`IMemoryCache`, ключ по пути+времени изменения файла),
поэтому бросок происходит один раз — при первом обращении к правилам на старте.

⚠️ Отдельно `FirebirdParseException`/наследников **никто не ловит** — их перехватывает общий
`catch (Exception)` в `ValidateParserConfiguration`. Поэтому диагностические поля (`RuleName` и др.)
в рантайме не используются; они видны только в тексте/логе исключения. Если понадобится показывать
пользователю адресную ошибку (какое правило и почему) — здесь и стоит ловить базовый тип.

---

## 6. Как расширять

- Новый вид ошибки библиотеки → **наследовать от `FirebirdParseException`** (а не от `Exception`),
  чтобы сохранить единый перехватываемый базовый тип.
- Диагностические данные класть в `init`-поля (как `RuleName`/`SampleData`), сообщение — в `base`.
- Если ошибка должна показываться пользователю адресно — добавить `catch (FirebirdParseException)`
  в `Program.ValidateParserConfiguration` (или там, где резолвится словарь правил) и разложить поля.

---

## Связанные файлы

| Роль | Файл |
|---|---|
| Определения исключений | `FirebirdTraceParser/Exceptions/FirebirdParseException.cs` |
| Единственный источник бросков | `FirebirdTraceParser/Parsing/Rules/JsonRuleLoader.cs` |
| Контракт загрузчика | `FirebirdTraceParser/Parsing/Rules/IRuleLoader.cs` |
| Ленивая загрузка правил (триггер) | `FirebirdTraceParser/Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` |
| Обработка на старте | `FirebirdTraceAnalyzer/Program.cs` (`ValidateParserConfiguration`) |
| Путь к правилам + сидирование | `FirebirdTraceAnalyzer/Services/RulesConfiguration.cs` (`EnsureRulesFile`) |
| Валидируемый файл правил | `%AppData%/FirebirdTraceAnalyzer/rules.json` — **пользовательская** копия; при первом запуске сидируется из поставляемого `<каталог приложения>/Configuration/rules.json` |
