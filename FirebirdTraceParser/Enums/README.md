# Перечисления: `FilterType`

Директория содержит перечисления модели парсера. Сейчас здесь один enum — **`FilterType`**:
он задаёт **тип контрола фильтра** для поля события и то, **как этот фильтр строится и рисуется**.

`FilterType` — часть декларативной системы фильтров: его выставляют в
[`FilterableFieldAttribute`](../Attributes/README.md) на свойстве модели события
(`[FilterableField("SQL", FilterType = FilterType.TextSearch)]`). Дальше значение проходит путь
`атрибут → DiscoveredField → FilteringService` и определяет, какой фильтр появится в UI.

Живёт в проекте **`FirebirdTraceParser`** (без зависимостей от Avalonia): на него ссылаются и
парсер, и плагины, и UI-проект.

---

## 1. Значения и что каждое значит

| Значение | Контрол в UI | Как строится (`FilteringService`) | Примечание |
|---|---|---|---|
| `Auto` | — | не строится напрямую: тип **выводится из CLR-типа свойства** методом `DetermineFilterType` (см. §2) | значение по умолчанию у атрибута |
| `EnumMultiSelect` | список чекбоксов (tri-state: не выбран → выбран → **исключён**) | `CreateEnumFilter` — перечисляет уникальные значения + счётчики | для enum-полей |
| `StringMultiSelect` | чекбоксы (топ-100 по частоте) + поле поиска по значениям | `CreateStringFilter` | для строк с небольшим числом различий (`User`, `Protocol`, …) |
| `NumericRange` | два поля «от / до» | `CreateNumericRangeFilter` — считает min/max | int/long/decimal/… |
| `DateTimeRange` | «от / до» по датам | `CreateDateTimeRangeFilter` | `DateTime` |
| `Boolean` | две галки (True/False) | маршрутизируется в `CreateEnumFilter`; дескриптор помечается `EnumMultiSelect` (чтобы обновление счётчиков и клон-предикат работали без отдельных веток) | `bool` |
| `TextSearch` | поле ввода «содержит подстроку» | `CreateTextSearchFilter` — предикат `Contains` (IgnoreCase) по `FilterDescriptor.SearchText` | одно поле, только подстрока; **не** regex и **не** мульти-поле (это глобальный поиск — см. [§8 README атрибутов](../Attributes/README.md)) |

---

## 2. `Auto` — вывод типа из свойства

Если `FilterType = Auto` (или не задан), `FilteringService.DetermineFilterType(propertyType)`
выбирает тип по CLR-типу (nullable разворачивается):

| Тип свойства | → `FilterType` |
|---|---|
| `enum` | `EnumMultiSelect` |
| `string` | `StringMultiSelect` |
| `DateTime` | `DateTimeRange` |
| `bool` | `Boolean` |
| числовой (`int`/`long`/`decimal`/`double`/`float`/`short`/`byte`) | `NumericRange` |
| прочее | `TextSearch` |

Поэтому `TextSearch` для SQL ставят **явно**: `Sql` — это `string`, и в `Auto` он ушёл бы в
`StringMultiSelect` (галочки по сотням тысяч уникальных SQL бесполезны).

---

## 3. Где значение используется (поток)

```
[FilterableField(FilterType = …)]          Attributes/  (метаданные на свойстве)
        │
        ▼
FieldDiscoveryService  →  DiscoveredField.FilterType         (рефлексия, кэш)
        │
        ▼
FilteringService.CreateFieldFilter(field)   switch по FilterType:
        │     Auto → DetermineFilterType, затем один из Create*-методов
        ▼
FilterDescriptor.FilterType                 (+ IsTextSearch = FilterType==TextSearch)
        │
        ├─ UI (FilterFlyout / ReportDesignerView): контрол выбирается ПО ДАННЫМ дескриптора —
        │    AvailableValues.Count>0 → чекбоксы; MinValue!=null → диапазон; IsTextSearch → поле ввода
        ├─ FilteringService.UpdateFilterValues / FiltersPanelViewModel.UpdateFilterCounts (пересчёт)
        └─ FilteringService.CreateConfigurableClone (клон фильтра для дизайнера отчётов)
```

Важно: **XAML не делает `switch` по `FilterType`** — контрол выбирается по наполнению дескриптора
(`AvailableValues`, `MinValue`, `IsTextSearch`). Поэтому если `Create*`-метод не заполнил ни
значений, ни диапазона, ни флага — поле визуально не появится.

---

## 4. Как добавить новое значение `FilterType` (чек-лист)

Просто добавить константу в enum **недостаточно** — иначе `CreateFieldFilter` вернёт `null`
(ветка `_ => null`), и фильтр не появится. Нужно провести значение по всей цепочке:

1. **`FilteringService.CreateFieldFilter`** — ветка `switch` → свой `CreateXxxFilter`.
2. **`CreateXxxFilter`** — построить `FilterDescriptor`: заполнить данные (значения/диапазон/предикат)
   и **обязательно передать `field.FilterDisplayOrder`** (иначе поле уедет в конец списка).
3. **UI** — путь отрисовки: либо задействовать существующий (`AvailableValues`/`MinValue`), либо
   завести флаг-видимости на дескрипторе (как `IsTextSearch`) и блок в `FilterFlyout.axaml`
   **и** `ReportDesignerView.axaml` (обе поверхности!).
4. **`UpdateFilterValues`** (в `FilteringService`) и **`UpdateFilterCounts`** (в `FiltersPanelViewModel`) —
   ветка, если нужен пересчёт значений/диапазонов при смене набора событий.
5. **`CreateConfigurableClone`** — `case` для привязки предиката к копии (иначе фильтр сломается в
   дизайнере отчётов).
6. **`DetermineFilterType`** — если новый тип должен выбираться в режиме `Auto`.

Пропуск любого из шагов 1–3 → фильтр «молча не работает».

---

## Связанные файлы

| Роль | Файл |
|---|---|
| Enum | `FirebirdTraceParser/Enums/FilterType.cs` |
| Где выставляется | `FirebirdTraceParser/Attributes/FilterableFieldAttribute.cs` (+ [README](../Attributes/README.md)) |
| Резолв `Auto` + построение | `FirebirdTraceAnalyzer/Services/Filtering/FilteringService.cs` |
| Рантайм-дескриптор | `FirebirdTraceAnalyzer/Services/Filtering/FilterDescriptor.cs` |
| UI фильтров | `FirebirdTraceAnalyzer/UserControls/FilterFlyout.axaml`, `.../Views/ReportDesignerView.axaml` |
| Состояние панели | `FirebirdTraceAnalyzer/ViewModels/FiltersPanelViewModel.cs` |
| Глобальный поиск (не путать) | `FirebirdTraceAnalyzer/Services/Searching/SearchService.cs` |
