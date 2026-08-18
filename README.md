# <img src="FirebirdTraceAnalyzer/Assets/app-logo-rounded.svg" width="30" height="30" alt="Firebird Trace Analyzer logo" />&nbsp; Firebird Trace Analyzer

**A cross-platform desktop toolkit for reading, exploring and reporting on Firebird trace & audit logs.**

[![Latest release](https://img.shields.io/github/v/release/deliciousNesquik/firebird-trace-analyzer?sort=semver)](https://github.com/deliciousNesquik/firebird-trace-analyzer/releases)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20macOS%20%7C%20Linux-blue)](#getting-started)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Avalonia](https://img.shields.io/badge/UI-Avalonia%2012-8B5CF6)](https://avaloniaui.net)
[![License: PolyForm NC 1.0.0](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-orange)](LICENSE.md)

<img src="https://github.com/user-attachments/assets/adb9cb09-3454-4411-b55b-6f2b1293a204" alt="Firebird Trace Analyzer overview" width="820">

### [**Read the documentation**](https://deliciousnesquik.github.io/firebird-trace-analyzer/)


---

Firebird's trace API is powerful, but its output is a firehose of flat text. **Firebird Trace Analyzer** turns those logs into something you can actually work with: it streams and parses trace files of any size, renders each event as a readable card, lets you filter / search / sort across millions of events, and turns the result into polished **PDF / DOCX / XLSX / CSV** reports.

It runs natively on **Windows, macOS and Linux**, loads logs from disk or straight off a server over **SSH**, and can optionally keep a local, deduplicated **event store** so you can re-open and analyze past sessions instantly.

## Highlights

- **Load logs multiple ways** — local files or remotely over **SSH/SFTP** (with trust-on-first-use host-key verification and secure credential storage).
- **Streaming parser** built for **large trace files** — parsing runs off the UI thread, so multi-million-event logs stay responsive.
- **Typed event cards** for every Firebird trace event type.
- **Rich filtering & search** — multi-select, numeric/date ranges, full-text search and stable sorting.
- **Optional local event store** (SQLite/WAL) with deduplication, storage analytics, on-demand compaction and a read-only SQL console.
- **Live report designer** — group, aggregate and preview, then export to **PDF, DOCX, XLSX or CSV**.
- **Extensible** via drop-in sort/filter plugins.
- **Light / Dark / Auto themes** and **English / Russian** localization.

## Getting started

### Download

Prebuilt builds for Windows, macOS and Linux are on the [**Releases**](https://github.com/deliciousNesquik/firebird-trace-analyzer/releases) page — download, unpack and run.

### Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/deliciousNesquik/firebird-trace-analyzer.git
cd firebird-trace-analyzer

dotnet run --project FirebirdTraceAnalyzer      # run
dotnet build FirebirdTrace.sln -c Release       # or build a release
dotnet test FirebirdTrace.sln                   # run the tests
```

## Documentation

Full usage guides — loading logs (locally and over SSH, every field explained), exploring & filtering, the event store, reports and plugins — live in the documentation site:

**https://deliciousnesquik.github.io/firebird-trace-analyzer/**

The site's source is in [`docs/`](docs/).

## Tech stack

.NET 10 · Avalonia 12 · CommunityToolkit.Mvvm · Microsoft.Data.Sqlite (WAL) · QuestPDF · ClosedXML · DocumentFormat.OpenXml · CsvHelper · SSH.NET · NLog

## Contributing

Issues and pull requests are welcome. For larger changes, please open an issue first to discuss the direction. Before submitting a PR, make sure the solution builds cleanly and all tests pass (`dotnet test FirebirdTrace.sln`).

## License

**Source-available** under the [**PolyForm Noncommercial License 1.0.0**](LICENSE.md): free to use, modify and share for **noncommercial** purposes. For commercial use, please contact the author.
