

# Firebird Trace Analyzer

**A cross-platform desktop toolkit for reading, exploring and reporting on Firebird trace & audit logs — without drowning in raw text.**

[![Latest release](https://img.shields.io/github/v/release/deliciousNesquik/firebird-trace-analyzer?sort=semver)](https://github.com/deliciousNesquik/firebird-trace-analyzer/releases)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20macOS%20%7C%20Linux-blue)](#-getting-started)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Avalonia](https://img.shields.io/badge/UI-Avalonia%2012-8B5CF6)](https://avaloniaui.net)
[![License: PolyForm NC 1.0.0](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-orange)](LICENSE.md)
[![Stars](https://img.shields.io/github/stars/deliciousNesquik/firebird-trace-analyzer?style=social)](https://github.com/deliciousNesquik/firebird-trace-analyzer/stargazers)

<!-- HERO GIF — вставь сюда обзорную гифку (можно любую из четырёх ниже) -->
<img src="https://github.com/user-attachments/assets/adb9cb09-3454-4411-b55b-6f2b1293a204" alt="Firebird Trace Analyzer overview" width="820">

Firebird's trace API is powerful but its output is a firehose of flat text. **Firebird Trace Analyzer** turns those logs into something you can actually work with: it streams and parses trace files of any size, renders each event as a readable card, lets you filter / search / sort across millions of events, and turns the result into polished PDF / DOCX / XLSX / CSV reports.

It runs natively on **Windows, macOS and Linux**, loads logs **from disk or straight off a server over SSH**, and can optionally keep a **local, deduplicated event store** so you can re-open and analyze past sessions instantly.

## Table of contents

- [Features](#-features)
- [Loading logs](#-loading-logs)
- [Explore & filter/sorting](#-explore--filter/sorting)
- [Local event store & SQL analytics](#-local-event-store--sql-analytics)
- [Reports](#-reports)
- [Getting started](#-getting-started)
- [Plugins](#-plugins)
- [Tech stack](#-tech-stack)
- [Contributing](#-contributing)
- [License](#-license)
- [Acknowledgements](#-acknowledgements)

## Features

- **Load logs three ways** — local files (dialog or drag & drop), remotely over **SSH/SFTP**, or re-opened from the local store.
- **Streaming parser** built for **large trace files** — events are parsed asynchronously off the UI thread, so multi-million-event logs stay responsive.
- **Typed event cards** for all Firebird trace event types (attach/detach, statement start/restart/finish, failed statements, procedures, triggers, errors, and more).
- **Rich filtering & search** — multi-select, numeric ranges, date/time ranges and full-text search, plus stable multi-key sorting.
- **Optional local event store** (SQLite/WAL) with deduplication, storage analytics, on-demand compaction and a **read-only SQL console**.
- **Live report designer** — group, aggregate (Count/Sum/Avg/…), sort and preview, then **export to PDF, DOCX, XLSX or CSV**.
- **Security-minded remote access** — TOFU host-key verification, OS-keychain credential storage with an encrypted fallback, and CSV formula-injection escaping.
- **Extensible** via drop-in sort/filter plugins.
- **Light / Dark / Auto themes** and **English / Russian** localization.

Open one or many trace files from disk (or drag & drop them onto the window). Parsing runs in the background with a live progress indicator and can be cancelled at any time.

<p align="center">
  <!-- GIF: локальная загрузка -->
  <img src="https://github.com/user-attachments/assets/adb9cb09-3454-4411-b55b-6f2b1293a204" alt="Loading trace files from disk" width="820">
</p>

### Remotely over SSH / SFTP

Connect straight to your Firebird host and pull trace logs over SFTP — no manual copying. Connections use **trust-on-first-use host-key verification**, credentials are stored in the OS keychain (with an encrypted local fallback), and reusable connection **profiles** make reconnecting a click. Optionally delete files from the server (or locally) after processing.

<p align="center">
  <!-- GIF: загрузка по SSH -->
  <img src="https://github.com/user-attachments/assets/4a907dfd-acd7-49fc-9816-2e1cc506f5a6" alt="Downloading trace logs over SSH" width="820">
</p>

## Explore & filter/sorting

Every event is rendered as a typed card, grouped by trace file. Slice the data with multi-select, numeric-range, date/time-range and text-search filters, sort by any discovered field, and jump around with full-text search — all computed off the UI thread so it stays smooth on huge logs.

<p align="center">
  <!-- GIF: фильтры / поиск / хранилище -->
  <img src="https://github.com/user-attachments/assets/ce84d793-fb59-4ef3-a4f2-0d9dfb927bcb" alt="Filtering, searching and the event store" width="820">
</p>


## Local event store & SQL analytics

Turn on the event store and Firebird Trace Analyzer keeps parsed sessions in a local **SQLite** database (WAL mode). It **deduplicates** repeated SQL text and connection metadata to keep the file small, lets you **re-open past sessions instantly**, and exposes a **read-only SQL console** for ad-hoc analytics that never blocks the write path. A one-click **compaction** reclaims disk space when you delete sessions.

## Reports

Build reports visually in the **live designer**: pick columns, mark them as plain fields, **group keys** or **aggregates** (Count / Sum / Avg / …), set sorting and filters, and watch a **WYSIWYG preview** update as you type. Save reusable templates (or start from the built-ins), then **export to PDF, DOCX, XLSX or CSV**. CSV exports are hardened against spreadsheet formula/DDE injection.

<p align="center">
  <!-- GIF: дизайнер отчётов + экспорт -->
  <img src="docs/media/reports.gif" alt="Designing and exporting a report" width="820">
</p>







## Getting started

### Download a release

Grab the latest build for your OS from the [**Releases**](https://github.com/deliciousNesquik/firebird-trace-analyzer/releases) page, unpack it, and run it — no installation required.

### Build from source

**Prerequisites:** the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/deliciousNesquik/firebird-trace-analyzer.git
cd firebird-trace-analyzer

# run the app
dotnet run --project FirebirdTraceAnalyzer

# or build a release
dotnet build FirebirdTrace.sln -c Release
```

Run the tests with:

```bash
dotnet test FirebirdTrace.sln
```

## Plugins

Firebird Trace Analyzer can be extended with **custom sort and filter plugins** — .NET assemblies dropped into the plugins folder and loaded on startup. Plugins can be enabled/disabled per instance, and version collisions between plugins with the same id are resolved automatically.

> Plugins run with the full privileges of the application. Only install plugins from sources you trust.

## Tech stack

| Area | Technology |
| --- | --- |
| Runtime | .NET 10 |
| UI | Avalonia 12 · CommunityToolkit.Mvvm · Fluent theme |
| Storage | Microsoft.Data.Sqlite (WAL) |
| Reports | QuestPDF (PDF) · DocumentFormat.OpenXml (DOCX) · ClosedXML (XLSX) · CsvHelper (CSV) |
| Remote | SSH.NET (SFTP) |
| Logging | NLog |
| Versioning | MinVer (git-tag based) |

## Contributing

Issues and pull requests are welcome. If you're planning a larger change, please open an issue first to discuss the direction. Before submitting a PR, make sure the solution builds cleanly and all tests pass (`dotnet test FirebirdTrace.sln`).

## License

This project is **source-available** under the [**PolyForm Noncommercial License 1.0.0**](LICENSE.md). You may use, modify and share it **for noncommercial purposes**. For commercial use, please contact the author.

## Acknowledgements

Built with [Avalonia](https://avaloniaui.net), [QuestPDF](https://www.questpdf.com), [ClosedXML](https://github.com/ClosedXML/ClosedXML), [SSH.NET](https://github.com/sshnet/SSH.NET), [CsvHelper](https://joshclose.github.io/CsvHelper/) and other great open-source libraries — and, of course, for the [Firebird](https://firebirdsql.org) community.
