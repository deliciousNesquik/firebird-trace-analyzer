# Утилиты разбора: `Parsing/Utils`

Вспомогательные утилиты этапа разбора трейс-файла. Живут в проекте **`FirebirdTraceParser`**
(без зависимостей от UI). Две независимые вещи:

| Файл | Назначение | Статус |
|---|---|---|
| `PerformanceTableParser.cs` | Разбор блока **статистики доступа к таблицам** (Natural/Index/…) из событий finish | Активно используется |
| `ParsingExtensions.cs` | Хелперы `Match` для безопасного чтения regex-групп | Активно используется (`DefaultEventHandler`) |

---

## 1. `PerformanceTableParser` — предметная область

Firebird в событиях завершения (`EXECUTE_STATEMENT_FINISH`, `EXECUTE_PROCEDURE_FINISH`,
`EXECUTE_TRIGGER_FINISH`) может печатать **таблицу статистики доступа** — сколько строк по каждой
затронутой таблице прочитано/изменено. Это ключевые данные для анализа производительности
(natural reads = полный перебор без индекса — красный флаг).

Пример реального блока (хвост события statement-finish):

```
    115 ms, 3531 read(s), 2 write(s), 3650 fetch(es), 16 mark(s)

Table                              Natural     Index    Update    Insert    Delete   Backout     Purge   Expunge
****************************************************************************************************************
RDB$INDICES                                       25
RDB$RELATIONS                                      2
HIS$MAIN                                           1                   1
KKM$CDNSERVERS                                    11         1
```

Особенности формата, которые определяют алгоритм:

- **Fixed-width, а не разделители.** Колонки выровнены по позициям слов в заголовке; значения
  right-align под своим столбцом. Пустая ячейка = столбец пропущен (счётчик 0).
- Строки данных и заголовок идут **flush-left** (с колонки 0), тело остального события —
  с отступом.
- Разделитель `****…` между заголовком и данными.
- Блок находится в **хвосте** тела события; заканчивается пустой строкой (или концом блока).

---

## 2. API

```csharp
public static PerformanceTable? ParsePerformanceTable(
    IReadOnlyList<string> lines, int startIndex,
    IReadOnlyDictionary<string, Regex> rules, ParsingContext context)
```

- `lines` — строки тела события; `startIndex` — откуда искать заголовок (парсер сам находит его дальше по тексту).
- `rules` — нужно правило **`performance_table_header`** (см. `rules.json`, §6).
- `context` — `ParsingContext` для интернирования имён таблиц (дедуп, см. связанные разделы).
- Возвращает `PerformanceTable { Items }` или **`null`**, если блока таблиц в событии нет
  (тогда `Items` не создаётся вовсе).

---

## 3. Алгоритм

1. Идём по строкам от `startIndex`.
2. Первая строка, матчащая `performance_table_header`, → `DetectColumnPositions` (позиции колонок
   по `IndexOf` слов Table/Natural/Index/Update/Insert/Delete/Backout/Purge/Expunge), `inTable = true`.
3. Строки с `***` — пропускаем.
4. Внутри таблицы: **пустая строка = конец блока** (`break`); иначе `ParseRow` по позициям.
5. `ParseRow` берёт срезы (`Slice`) фиксированной ширины и парсит числа (`ParseIntSafe`), имя
   таблицы интернируется через `context`.

Раскладка колонок (границы вычисляются из заголовка):

| Поле | Диапазон среза |
|---|---|
| `TableName` | `[0 … NaturalStart-1]` |
| `NaturalCount` | `[Natural … Index-1]` |
| `IndexCount` | `[Index … Update-1]` |
| `UpdateCount` | `[Update … Insert-1]` |
| `InsertCount` | `[Insert … Delete-1]` |
| `DeleteCount` | `[Delete … Backout-1]` |
| `BackoutCount` | `[Backout … Purge-1]` |
| `PurgeCount` | `[Purge … Expunge-1]` |
| `ExpungeCount` | `[Expunge … конец строки]` |

---

## 4. Краевые случаи

- **Нет блока** в событии → `Items` пуст → возвращается `null` (в модели это отличают от «таблица
  есть, но строк нет» — см. `perf_table_state` в хранилище).
- **Пустые ячейки** — `ParseIntSafe` на пустом срезе даёт `0`.
- **Строка короче** правой границы — `Slice` защищён от выхода за длину (недостающие колонки → 0).
- **Битая строка** — `ParseRow` ловит исключение, логирует warning и пропускает строку (не роняет
  разбор всего файла).

---

## 5. Терминатор: почему «конец = пустая строка»

Прежнее условие «конец таблицы = строка без отступов» ошибочно срабатывало на **самой первой
строке данных** (они flush-left, как `RDB$INDICES`), поэтому таблица **никогда не парсилась** —
`perf_table_item` в хранилище всегда был пуст. Исправлено: строки данных идут подряд, границей
блока считается пустая строка.

---

## 6. Где вызывается и с чем связано

- **`DefaultEventHandler`** (`Parsing/Handlers`) вызывает парсер для трёх типов finish-событий:
  statement, **procedure**, **trigger**. (Изначально вызывался только для statement — из-за чего у
  процедур/триггеров таблицы не разбирались; теперь во всех трёх.) Результат кладётся в событие,
  только если включён флаг `ParseOptions.ParsePerformanceTables`.
- **`rules.json`** — правило `performance_table_header` (шаблон строки-заголовка). Само значение
  строк парсится fixed-width'ом, а не regex.
- **Модели** `PerformanceTable` / `PerformanceTableItem` (`Models/ValueObjects`) — форма результата.
- **`ParsingContext`** (`Infrastructure/Caching`) — интернирование `TableName` (имена таблиц
  повторяются массово между событиями).
- В **хранилище** статистика лежит в таблице `perf_table_item` (запись — `EventStoreService`),
  а «есть ли таблица и со строками ли» кодирует `perf_table_state` (0 нет / 1 есть без строк /
  2 со строками).

---

## 7. `ParsingExtensions`

Три метода-расширения над `System.Text.RegularExpressions.Match` для безопасного чтения групп.
Числовые перегрузки читают `ValueSpan` (без аллокации строки), поэтому годятся для горячего пути
парсера; на отсутствующей/несовпавшей группе возвращают `defaultValue`, а не бросают:

| Метод | Назначение |
|---|---|
| `string GetGroupValue(this Match, string groupName, string defaultValue = "")` | значение группы или `defaultValue`, если группа не совпала |
| `int GetGroupInt(this Match, string groupName, int defaultValue = 0)` | целое из `ValueSpan` (без аллокации) через `int.TryParse` или `defaultValue` |
| `long GetGroupLong(this Match, string groupName, long defaultValue = 0)` | long из `ValueSpan` (без аллокации) через `long.TryParse` или `defaultValue` |

**Статус:** используются в `DefaultEventHandler` для всех чтений regex-групп (заменили прямой
`match.Groups[...].Value` и локальные `ParseIntOrDefault`/`ParseLongOrDefault`). Единственное
исключение — `m.Groups["params"].ValueSpan` при разборе SQL-параметров: там нужен срез span, а не
чтение значения группы, поэтому доступ остаётся прямым.

Замена эквивалентна прежнему поведению: `GetGroupValue` == `.Value` для совпавших групп, а
`GetGroupInt`/`GetGroupLong` над `ValueSpan` дают тот же результат, что и старые span-хелперы
(несовпавшая группа → пустой span → `defaultValue`).
