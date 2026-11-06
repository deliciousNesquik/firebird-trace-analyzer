# AGENTS.md

Guidance for AI coding agents working in this repository.

## What this is

Desktop tool for parsing and analyzing **Firebird 5.x trace logs** (`fbtrace`). Users open a trace
file (local or downloaded over SSH/SFTP), the parser turns it into typed events, and the Avalonia UI
provides sort/filter/search, per-event detail cards, an optional SQLite event store, and report
export (PDF/CSV/DOCX/XLSX). Licensed PolyForm Noncommercial — keep that in mind, no commercial-use code.

## Commands

Requires the **.NET 10 SDK** (targets `net10.0`; parser uses `LangVersion=preview`).

```bash
dotnet build FirebirdTrace.sln                  # build everything
dotnet test FirebirdTrace.sln                   # run all tests (xUnit)
dotnet test FirebirdTraceParser.Tests           # focused: parser tests
dotnet test FirebirdTraceAnalyzer.Tests         # focused: app-side tests
dotnet run --project FirebirdTraceAnalyzer      # launch the desktop app
./build/build.sh [mac|win|linux] [version]      # self-contained release builds → build/artifacts/
```

Local `build.sh` runs without a `[version]` arg fall back to **1.0.0** (the script reads
`<Version>` from the csproj, which is deliberately unset; `MAC_RID` env picks the osx RID).

Docs site (Astro + Starlight, Node ≥ 22), run from `docs/`: `npm run dev` / `npm run build`.

## CI & releases

Default branch is `master`. Two workflows in `.github/workflows/`:

- **Release** — on pushing a `v*` tag. Runs the same `build/build.sh` on a matrix (macos-14 builds
  both osx RIDs, ubuntu cross-builds win/linux) and uploads archives to a GitHub Release; tags with
  `-alpha`/`-beta` become pre-releases.
- **Docs** — on push to `master` touching `docs/`, deploys the Starlight site to GitHub Pages.

Versions come from git tags via **MinVer** (`MinVerTagPrefix=v`); CI derives the version from the tag
— never hardcode `<Version>` in the csproj.

## Solution layout

| Project | Role |
|---------|------|
| `FirebirdTraceParser` | Pure parsing engine + event models. **No UI deps.** `TreatWarningsAsErrors` (CS1591 suppressed), XML docs generated, `InternalsVisibleTo` the parser tests. |
| `FirebirdTraceAnalyzer` | Avalonia 12 desktop app (MVVM, CommunityToolkit.Mvvm). References the parser. |
| `TemplatePlugin` | Example sort/filter plugin; references both projects. |
| `FirebirdTraceParser.Tests` / `FirebirdTraceAnalyzer.Tests` | xUnit; parser tests **link `rules.json` from the app** so tests can't drift from shipped rules. |

Docs live in `docs/` (Starlight, bilingual: every EN page under `docs/src/content/docs/**` has an RU
mirror under `docs/src/content/docs/ru/**` — update both sides together).

## Parser architecture (FirebirdTraceParser)

Regex-rule-driven, block-based pipeline:

1. **Rules live in `FirebirdTraceAnalyzer/Configuration/rules.json`** (named regexes with capture
   groups, validated against `rules.schema.json` by `JsonRuleLoader`). To support a new log line
   shape, edit rules.json — don't hardcode regexes in C#.
   **Gotcha**: at startup `RulesConfiguration.EnsureRulesFile()` seeds
   `%AppData%/FirebirdTraceAnalyzer/rules.json` from the bundled copy **only if missing**, and the
   app parses *that* user copy. Editing the repo's rules.json does nothing for an already-seeded
   local install — delete the AppData copy to re-seed. Parser **tests**, however, link the repo
   file, so tests and a locally-run app can see different rules.
2. `TraceLogParser` reads line by line, detects event boundaries via `block_header`, buffers lines
   between headers. Sync/async/streaming entry points (`ParseFile`, `ParseFileAsync`,
   `ParseStreamAsync`).
3. `DefaultEventHandler` dispatches on `event_type` to build typed events; all derive from
   `EventBase` (`Models/Events`), value objects in `Models/ValueObjects`.
4. **Hot paths are allocation-sensitive**: interning lives in `ParsingContext`
   (`Infrastructure/Caching/`: `Intern`, `InternSession`, attachment dedup) — route new repeatable
   strings through `context.Intern(...)`. Span/slicing idioms are deliberate — preserve them.
5. Parse failures don't throw; they become `ParsingWarning`s collected in `ParsingResult<EventBase>`.

DI entry: `services.AddFirebirdTraceParser(rulesPath, nlogConfigPath)`. The parser ships its own
READMEs (`FirebirdTraceParser/README.md` plus per-directory, e.g. `Infrastructure/Caching/README.md`)
— read them before changing the engine.

## App architecture (FirebirdTraceAnalyzer)

- **DI is registered in one place**: `Program.ConfigureServices()`. `appsettings.json` binds via the
  options pattern; startup aborts if zero parsing rules load.
- **Columns are reflection-driven**: `FieldDiscoveryService` scans event properties for
  `[SortableField]` / `[FilterableField]` (defined in the parser) to build UI field lists. To expose
  a new sortable/filterable column, annotate the model property — no UI change needed.
- `MainWindowViewModel` is a large central ViewModel. `AllEvents` is the source of truth;
  `VisibleEvents` (Core/RangeObservableCollection, batch via `ReplaceRange`) is the
  search→filter→sort projection recomputed by `ApplyAllFilters()` on the UI thread.
- Services groups under `Services/`: Sorting, Filtering, Searching, EventProperties, Reports,
  Persistence (SQLite event store), Plugins. SSH remote access via SSH.NET; credentials via
  DPAPI/`ProtectedData` and `CredentialStorageService`.
- Reports: templates are **JSON** (`ReportTemplate` model — built-ins in
  `Services/Reports/BuiltInTemplates/`, user templates in
  `%AppData%/FirebirdTraceAnalyzer/Templates/Custom`), rendered by `IReportExporter`
  implementations (QuestPDF=PDF, ClosedXML=XLSX, OpenXml=DOCX, CsvHelper=CSV).
- **Theming**: Light/Dark/Contrast/Auto applied by `ThemeService`; brushes in `Themes/Light.axaml`,
  `Themes/Dark.axaml`, `Themes/Contrast.axaml` (incl. per-event-type card colors).
- **Localization**: UI strings resolve via `loc:Tr` markup / `Loc.Tr(...)` from `Assets/i18n/en.json`
  and `ru.json` — a new UI string must be added to both files.
- **Compiled bindings by default** (`AvaloniaUseCompiledBindingsByDefault=true`): XAML binding
  paths are checked at build time; views need `x:DataType`, and reflection-style binding strings
  fail the build instead of silently resolving to nothing.

## Plugin system

`PluginManagerService` scans `%AppData%/FirebirdTraceAnalyzer/Plugins/<subdir>/*.dll` — **subfolders
only**, a DLL directly in `Plugins/` is ignored — and loads each via a collectible
`PluginLoadContext`, instantiating `IAnalyzerPlugin` implementations; `ISortPlugin` and
`IFilterPlugin` (defined in the app project, `Interfaces/Plugins/`) are the extension points. The
loader skips `FirebirdTraceParser.dll` to avoid re-scanning the SDK. Enable/disable state persists
in `plugins.state.json` keyed by (DLL path, plugin Id) — not by Id alone, since plugin Ids can
collide. New plugins mirror `TemplatePlugin/`.

## Conventions & gotchas

- **Language**: code comments and git commit messages are in **Russian**; write conversation/docs in
  English unless asked otherwise. Commits use conventional prefixes — `feat(parser):`,
  `feat(ui):`, `style(ui):` … — with a Russian summary.
- **macOS native menu** (`Views/NativeMainMenu.cs`): mirrors the in-window `MainMenuView` and is
  set with `NativeMenu.SetMenu` **exactly once**; dynamic items (report lists) are updated only
  inside the `NativeMenu.NeedsUpdate` handler. Re-calling `SetMenu` throws on macOS, and mutating
  `Items` outside `NeedsUpdate` desyncs the exporter and makes the menu inert. `NativeMenu` has no
  `ItemsSource`; Settings is declared declaratively at Application level in `App.axaml`; submenus
  must embed `<NativeMenuItem.Menu><NativeMenu>` or the exporter throws NRE. On Windows/Linux
  `SetMenu` is a no-op (the in-window menu is used).
- **SVG icon theming**: icon SVG paths must carry `class="ThemeIconStroke"` / `"ThemeIconFill"` to
  recolor with the theme; icons are `x:String` resource keys in `App.axaml` used via
  `Svg Path="{DynamicResource …}"`.
- **EventCards** (`Controls/EventCards`) are ~15 independent `TemplatedControl`s with heavy
  duplication and no shared base — match the existing pattern; copy buttons read from the event
  DataContext, not the styled properties.
- **No IL trimming** — reflection (field discovery) would break. Releases are
  self-contained single-file for win-x64 / osx-x64 / osx-arm64 / linux-x64; the macOS `.app` bundle
  is only produced when building on macOS (`build/build.sh`).
- **NLog**: log file paths are injected from C# (`LogConfiguration` sets GDC items; default
  `%AppData%/FirebirdTraceAnalyzer/logs`) — editing `fileName` in `Configuration/nlog.config` alone
  won't relocate logs, and parser logs are routed to a separate file by exact-logger-name rules.

## Read before touching sensitive areas

- `docs/src/content/docs/sdk/` — plugin authoring contracts (update the RU mirror too).
- `build/README.md` — release/build pipeline details before changing publishing.
- `CLAUDE.md.local` — older, deeper notes on the same architecture; partially outdated (predates
  test projects and ThemeService), verify specifics against code.
