# Атрибуты полей: `FilterableFieldAttribute` и `SortableFieldAttribute`

Декларативные метки на свойствах модели событий парсера. Помечают, какие поля события
доступны для **фильтрации** и **сортировки** в UI, и как они там называются, группируются и
упорядочиваются. Приложение обнаруживает эти поля рефлексией и само строит списки фильтров и
сортировок — **без ручного кода в UI на каждое новое поле**.

- `FilterableFieldAttribute` — свойство можно фильтровать.
- `SortableFieldAttribute` — по свойству можно сортировать.

Оба живут в проекте **`FirebirdTraceParser`** (не в UI-проекте) намеренно: на них ссылаются и
парсер, и плагины, а зависимости от Avalonia там быть не должно.

---

## 1. Что это и зачем

Модель события (`EventBase` и подтипы, а также value-объекты вроде `AttachmentInfo`,
`PerformanceInfo`, `TransactionInfo`) — это обычные C#-классы. Чтобы поле стало доступно в
панели фильтров или во флайауте сортировки, его свойство помечают атрибутом. Дальше всё
происходит автоматически:

```
Модель событий (FirebirdTraceParser)                     ← ВЫ ЗДЕСЬ (метаданные)
  EventBase, StatementEvents, TriggerEvents, ErrorEvent, …
  value-объекты: AttachmentInfo, PerformanceInfo, TransactionInfo, …
        │   свойства помечены
        │   [FilterableField(...)] / [SortableField(...)]
        ▼
  FieldDiscoveryService.ScanProperties()                 (FirebirdTraceAnalyzer/Services)
        │   рефлексия по типам событий (кэш на тип);
        │   рекурсивный обход вложенных value-объектов (глубина ≤ 3)
        ▼
  DiscoveredField                                        (FirebirdTraceAnalyzer/Models)
        │   { DisplayName, Category, PropertyPath, PropertyType,
        │     IsFilterable, IsSortable, FilterType,
        │     FilterDisplayOrder, SortDisplayOrder, IsDefaultSort }
        ├──────────────────────────────┬──────────────────────────────┐
        ▼                              ▼
  GetFilterableFields()          GetSortableFields()
        │                              │
        ▼                              ▼
  FilteringService                SortingService
    CreateFieldFilter()             CreateFieldSort()
        │                              │
        ▼                              ▼
  FilterDescriptor                SortDescriptor
    { Category, DisplayOrder, … }    { Category, DisplayOrder, IsDefault, … }
        │        OrderBy(Category).ThenBy(DisplayOrder)
        ▼
  UI: панель фильтров / флайаут сортировки
```

Ключевая идея: **атрибут — единственное место, которое нужно тронуть**, чтобы новое поле
появилось в фильтрах/сортировках. Сервисы обнаружения и построения списков трогать не нужно.

---

## 2. Справочник свойств

### `FilterableFieldAttribute`

| Свойство | Тип | По умолчанию | Назначение |
|---|---|---|---|
| `DisplayName` | `string` (аргумент конструктора) | — (обязателен) | Подпись поля в UI |
| `Category` | `string` | `"General"` | Группа в списке фильтров |
| `FilterType` | `FilterType` | `Auto` | Тип контрола фильтра (см. §3) |
| `DisplayOrder` | `int` | `100` | Порядок в списке фильтров, **меньше = выше**. Это UI-порядок, не логика |

### `SortableFieldAttribute`

| Свойство | Тип | По умолчанию | Назначение                                                     |
|---|---|---|----------------------------------------------------------------|
| `DisplayName` | `string` (аргумент конструктора) | — (обязателен) | Подпись поля в UI                                              |
| `Category` | `string` | `"General"` | Группа в списке сортировок                                     |
| `DisplayOrder` | `int` | `100` | Порядок в списке сортировок, **меньше = выше**                 |
| `IsDefault` | `bool` | `false` | Сортировка по умолчанию. **Лучше не использовать в плагинах!** |

`DisplayName` обязателен: конструктор бросает `ArgumentNullException` при `null`.

---

## 3. `FilterType` и авто-определение

`FilterType` (enum `FirebirdTraceParser.Enums.FilterType`) задаёт тип контрола фильтра. При
`Auto` тип выводится из типа свойства методом `FilteringService.DetermineFilterType`:

| Тип свойства | → `FilterType` | Контрол                     |
|---|---|-----------------------------|
| `enum` | `EnumMultiSelect` | Список чекбоксов            |
| `string` | `StringMultiSelect` | Множественный выбор + поиск |
| `DateTime` | `DateTimeRange` | Диапазон дат                |
| `bool` | `Boolean` | Два варианта (True/False)   |
| числовой (`int`/`long`/`decimal`/…) | `NumericRange` | Числовой диапазон           |
| прочее | `TextSearch` | Поле «содержит» (подстрока) |

`TextSearch` обычно ставят **явно** (напр. для SQL — высокая кардинальность, галочками не выбрать);
в `Auto` он — лишь запасной вариант для нетипичных типов. Про его отличие от глобального поиска — §8.

---

## 4. Как использовать

Пометить свойство модели события. Пример из `EventBase`:

```csharp
[SortableField("Time", Category = "General", IsDefault = true)]
[FilterableField("Time", Category = "General", FilterType = FilterType.DateTimeRange)]
public DateTime Timestamp { get; init; }
```

Свойство может нести **оба** атрибута (и фильтруемое, и сортируемое) или только один.
Пример из value-объекта `AttachmentInfo` (поле попадёт в UI под путём `Attachment.DatabasePath`):

```csharp
[FilterableField("Database Path", Category = "Attachment", FilterType = FilterType.StringMultiSelect)]
public string DatabasePath { get; init; }
```

Порядок и группировка:

```csharp
// Сначала по категории (алфавит), внутри — по DisplayOrder (меньше = выше).
[FilterableField("Trace ID", Category = "General", DisplayOrder = 10)]
[FilterableField("Component", Category = "Error",  DisplayOrder = 20)]
```

**Больше ничего менять не нужно** — ни в `FieldDiscoveryService`, ни в `FilteringService`/
`SortingService`, ни в UI. Поле появится автоматически после перезапуска (см. §5 про кэш).

---

## 5. Как это обнаруживается (детали механики)

`FieldDiscoveryService.ScanProperties` (в проекте `FirebirdTraceAnalyzer`):

- Идёт рефлексией по публичным свойствам типа события.
- **Вложенные value-объекты** (напр. `AttachmentInfo`) разворачиваются рекурсивно; сам
  контейнер полем не становится. Путь свойства — с точками: `Attachment.User`. Глубина
  ограничена `MaxScanDepth = 3`.
- Для каждого свойства читает оба атрибута и заполняет `DiscoveredField`:
  `Category`, `DisplayName`, `FilterType`, `FilterDisplayOrder`, `SortDisplayOrder`,
  `IsFilterable`, `IsSortable`, `IsDefaultSort` (из `SortableFieldAttribute.IsDefault`).
- `DisplayName` берётся из атрибута; если поле без атрибута попало в обход — подставляется
  `FormatPropertyName` (PascalCase → «Pascal Case»).
- Результат **кэшируется по типу** (`_fieldCache`) и по набору типов (`_commonFieldsCache`).
  Изменения в атрибутах видны после перезапуска приложения или `ClearCache()`.

Списки строят `FilteringService.GetAvailableFilters` и `SortingService.GetAvailableSorts`:
каждое поле → `FilterDescriptor`/`SortDescriptor`, финальная сортировка списка —
`OrderBy(Category).ThenBy(DisplayOrder)`. При равном `DisplayOrder` порядок — как объявлены
свойства (стабильная сортировка порядка обнаружения).

---

## 6. Где править и почему именно так

- **Добавить поле в фильтры/сортировки** → повесить атрибут на свойство модели события. Это
  единственная точка правки; сервисы и UI трогать не нужно (в этом весь смысл декларативности).
- **Переименовать/перегруппировать/переупорядочить** → менять `DisplayName`/`Category`/
  `DisplayOrder` в атрибуте. Это чисто UI-метаданные, на логику разбора/хранения не влияют.
- **`DisplayOrder` — почему два в `DiscoveredField`** (`FilterDisplayOrder` и `SortDisplayOrder`):
  фильтры и сортировки — два независимых списка, у каждого атрибута свой `DisplayOrder`; поле
  может нести оба атрибута с разными значениями, поэтому они не схлопываются в одно.
- **Почему атрибуты в проекте парсера, а не в UI:** и парсер, и плагины ссылаются на модель
  событий; тянуть в них Avalonia нельзя. Рантайм-дескрипторы (`FilterDescriptor`/`SortDescriptor`)
  и логика построения списков живут уже в UI-проекте (`FirebirdTraceAnalyzer`).

---

## 7. Связь с плагинами

Плагины **не используют** эти атрибуты: они отдают готовые `FilterDescriptor`/`SortDescriptor`
напрямую (со своими `DisplayName`/`Category`/`DisplayOrder`). Атрибуты — механизм для
**встроенной** модели событий парсера. Нейминг `DisplayOrder` в атрибутах намеренно совпадает
с `DisplayOrder` дескрипторов — это один и тот же UI-порядок «меньше = выше», дефолт `100`.

---

## 8. Поиск: глобальный vs фильтр по полю (`TextSearch`)

В приложении есть **два разных механизма поиска по тексту** — их важно не путать. Они **намеренно
раздельны и не объединяются**.

| | **Фильтр по полю** (`FilterType.TextSearch`) | **Глобальный поиск** (строка сверху) |
|---|---|---|
| Где | панель фильтров **и** дизайнер отчётов | только главное окно |
| Область | **одно** поле (`PropertyPath`, напр. `Sql`) | **несколько** полей сразу: `Sql` + `ProcedureName` + `TriggerName` |
| Режим | только подстрока (`Contains`, IgnoreCase) | **два**: Classic (`Contains`) и **Regex** (`Regex.IsMatch`) |
| Роль | сущность-фильтр: комбинируется с другими, сохраняется в конфиге отчёта | разовый вид-фильтр поверх списка, не сохраняется |
| Реализация | `FilteringService.CreateTextSearchFilter` → предикат по `FilterDescriptor.SearchText` | `SearchService` (`SearchClassic` / `SearchRegex`) |

**Почему не объединили.** Глобальный поиск умеет **regex** и ищет **по нескольким полям сразу** —
фильтр по полю ни того, ни другого не делает (и не должен: он про «одно поле содержит подстроку»,
зато комбинируется и живёт в отчётах). Схлопывание глобального поиска в `TextSearch`-фильтр
**сломало бы regex и мульти-поле**, поэтому они оставлены раздельно.

Если когда-нибудь понадобится **regex в фильтре по полю** — это делается *добавлением* режима
(Classic/Regex) фильтру через общий матчер, а не слиянием с глобальным поиском (глобальный при этом
не трогается).

---

## Связанные файлы

| Роль | Файл |
|---|---|
| Атрибут фильтрации | `FirebirdTraceParser/Attributes/FilterableFieldAttribute.cs` |
| Атрибут сортировки | `FirebirdTraceParser/Attributes/SortableFieldAttribute.cs` |
| Enum типов фильтра | `FirebirdTraceParser/Enums/FilterType.cs` |
| Обнаружение полей | `FirebirdTraceAnalyzer/Services/FieldDiscoveryService.cs` |
| DTO обнаруженного поля | `FirebirdTraceAnalyzer/Models/DiscoveredField.cs` |
| Построение фильтров | `FirebirdTraceAnalyzer/Services/Filtering/FilteringService.cs` |
| Построение сортировок | `FirebirdTraceAnalyzer/Services/Sorting/SortingService.cs` |
| Рантайм-дескрипторы | `.../Filtering/FilterDescriptor.cs`, `.../Sorting/SortDescriptor.cs` |
| Панель фильтров (состояние/UI) | `.../ViewModels/FiltersPanelViewModel.cs`, `.../UserControls/FilterFlyout.axaml` |
| Глобальный поиск (classic/regex) | `FirebirdTraceAnalyzer/Services/Searching/SearchService.cs` |
