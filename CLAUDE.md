# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Desktop tool for parsing and analyzing **Firebird database trace logs**. A user opens a
trace file (locally or downloaded over SSH), the parser turns it into a stream of typed
events, and the Avalonia UI lets you sort/filter/search them, view per-event detail cards,
and export reports (PDF/CSV/DOCX/XLSX).

## Build & run

Requires the **.NET 10 SDK** (projects target `net10.0`, parser uses `LangVersion=preview`).

```bash
dotnet build FirebirdTrace.sln                 # build all 3 projects
dotnet run --project FirebirdTraceAnalyzer      # launch the desktop app
dotnet publish FirebirdTraceAnalyzer -c Release -r win-x64   # single-file self-contained exe
```

- **No test project exists** — there is no `dotnet test` target.
- `FirebirdTraceParser` has `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — any
  compiler warning fails the build for that project (XML doc warning CS1591 is suppressed).
- The app's `RuntimeIdentifiers` is `win-x64` and publish is single-file/self-contained;
  Avalonia runs cross-platform for local dev but releases are Windows.
- Code comments and git commit messages in this repo are in **Russian**; per global
  conventions, write new docs/conversation in English unless asked otherwise.

## Solution layout

Three projects (`FirebirdTrace.sln`):

| Project | Type | Role |
|---------|------|------|
| `FirebirdTraceParser` | class library | Pure parsing engine + event models. No UI deps. |
| `FirebirdTraceAnalyzer` | Avalonia 12 desktop app | MVVM UI, references the parser. |
| `TemplatePlugin` | class library | Example plugin; references both above. |

## Parser architecture (`FirebirdTraceParser`)

The parsing pipeline is **regex-rule-driven and block-based**:

1. **Rules** live in `FirebirdTraceAnalyzer/Configuration/rules.json` — a dictionary of named
   regex patterns (with named capture groups, flags, `requiredGroups`, and a `sample`).
   `JsonRuleLoader` loads them, validating against `rules.schema.json` (NJsonSchema), and
   returns `IReadOnlyDictionary<string, Regex>`. To support a new log line shape, add/adjust
   a rule here rather than hardcoding regexes in C#.
2. **`TraceLogParser`** (`Parsing/Engine`) reads the file line by line, using the
   `block_header` rule to detect event boundaries. Lines between headers accumulate into a
   block buffer; on the next header (or EOF) the block is flushed. Offers sync `ParseFile`,
   `ParseFileAsync` (with `IProgress<double>`), and streaming `ParseStreamAsync`
   (`IAsyncEnumerable`, batched).
3. **`DefaultEventHandler`** (`Parsing/Handlers`) receives each block, reads `event_type`
   from the header, and dispatches via a `switch` to build a strongly-typed event. All events
   derive from `EventBase` (subtypes in `Models/Events`: Trace init/fini, Attach/Detach,
   Statement/Procedure/Trigger start/finish, their `Failed*` variants, restart, error). Value
   objects (`Models/ValueObjects`) hold `AttachmentInfo`, `TransactionInfo`, `PerformanceInfo`,
   SQL params, etc.
4. **Interning pools** (`Infrastructure/Caching`): `StringPool`, `AttachmentPool`,
   `TraceSessionPool` dedupe repeated strings/objects across the (potentially huge) log to cut
   allocations. The hot parse paths deliberately use `ValueSpan`/slicing to avoid allocations
   (see `ParseTransactionInfo`). Preserve this style when editing the handler.
5. Parse failures don't throw out of the parser — they become `ParsingWarning`s collected
   alongside events in `ParsingResult<EventBase>`. Severity depends on `ParseOptions.ValidationMode`.

Wire the parser into DI with `services.AddFirebirdTraceParser(rulesPath, nlogConfigPath)`
(`Infrastructure/DependencyInjection`). The parser is registered `Transient`; rules and the
handler are singletons.

## App architecture (`FirebirdTraceAnalyzer`)

- **MVVM** with `CommunityToolkit.Mvvm`. ViewModels in `ViewModels/`, views in `Views/` +
  `UserControls/`, reusable controls in `Controls/` (per-event detail UI in `Controls/EventCards`).
  `ViewLocator` maps ViewModels → Views. Compiled bindings are on by default.
- **DI is set up in `Program.ConfigureServices()`** — the single place services are registered.
  `appsettings.json` is bound to `AppSettings`/`UiSectionSettings` via the options pattern.
  Startup calls `ValidateParserConfiguration` and aborts if zero rules loaded.
- **Reflection-driven fields**: `FieldDiscoveryService` scans event model properties for
  `[SortableField]` / `[FilterableField]` attributes (defined in the parser's `Attributes/`)
  to dynamically build the sort/filter/search field lists shown in the UI — including the
  intersection of fields common to the currently loaded event types. To expose a new column
  for sorting/filtering, annotate the model property; no UI change needed.
- **Service groups** under `Services/`: `Sorting`, `Filtering`, `Searching`,
  `EventProperties` (reflection property access), and `Reports`. Remote-file features use
  `SshConnectionService`/`RemoteFileService` (SSH.NET) and `CredentialStorageService`
  (Windows DPAPI via `ProtectedData`).
- **Reports** (`Services/Reports`): templates (`ReportTemplateService`, RazorLight) rendered
  and exported through `IReportExporter` implementations — PDF (QuestPDF), CSV (CsvHelper),
  DOCX (OpenXml), XLSX (ClosedXML). Built-in templates in `BuiltInTemplates`.

## Plugin system

`PluginManagerService` scans `%AppData%/FirebirdTraceAnalyzer/Plugins/<subdir>/*.dll`, loads
each via `PluginLoadContext` (an isolated `AssemblyLoadContext`), and instantiates any type
implementing `IAnalyzerPlugin`. Two extension points exist today: `ISortPlugin`
(`GetSorts()` → `SortDescriptor`s) and `IFilterPlugin` (stub for the future). The loader
deliberately skips `FirebirdTraceParser.dll` so a plugin's copy of the SDK isn't re-scanned.

**To write a plugin**, mirror `TemplatePlugin/`: a `net10.0` library referencing
`FirebirdTraceAnalyzer` + `FirebirdTraceParser`, implementing the relevant interface, then
drop its build output in a subfolder of the plugins directory above.
