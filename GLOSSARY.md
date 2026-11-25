# Firebird Trace Analyzer

Desktop tool for parsing and analyzing Firebird trace logs: a trace file is parsed into typed
events, which are then filtered, searched, sorted and exported to reports. This glossary fixes
the language used in the UI, code and docs.

## Language

**Trace event**:
A typed record (statement start, fetch, commit, error, …) produced by parsing one block of a
trace log. All events derive from `EventBase`.
_Avoid_: log line, record

**Trace block**:
A contiguous group of log lines between two block headers — the unit the parser consumes as a
whole. A block normally yields one event.
_Avoid_: chunk, section

**ParsingWarning**:
The parser's per-problem record for content it could not fully process, carrying `Severity`
(Error / Warning / Info), message and line number. Lives in `ParsingResult.Warnings` and
`ParsedFile.Warnings`; per-line details go to parser.log.
_Avoid_: parse error, log warning

**Skipped block** («пропущенный блок»):
A trace block the parser failed to turn into an event; its events are missing from the app.
Counted as a `ParsingWarning` with Severity ≥ Warning — this single number is what the UI
surfaces. For files loaded from the store cache the count is unknown (not persisted), shown
as «—»/`null`, never as zero.
_Avoid_: parse error, failed block, bad line

**Produce** (phase):
Obtaining the working set of events for a file — by parsing it fresh or by reading it from
the store cache (`FileParseMetric.FromCache` distinguishes the two).
_Avoid_: loading (ambiguous between download/parse/read)

**Store cache**:
The SQLite event store that lets a previously parsed file reopen without re-parsing, keyed
by file hash.
_Avoid_: session, snapshot
