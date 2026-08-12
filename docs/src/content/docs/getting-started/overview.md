---
title: Overview
description: What Firebird Trace Analyzer is, who it's for, and what it can do.
---

**Firebird Trace Analyzer** is a cross-platform desktop application for reading, exploring
and reporting on [Firebird](https://firebirdsql.org) trace & audit logs. It runs natively on
**Windows, macOS and Linux**.

## Why

Firebird's trace API produces flat, verbose text that's hard to read and harder to analyze.
This app parses that output into structured, typed **events**, lets you slice them with
filters and search, and turns the result into shareable reports — without writing scripts or
loading everything into a spreadsheet.

<figure class="fta-figure">
  <img src="/firebird-trace-analyzer/shared/raw-trace-log.png" alt="Raw Firebird trace log text" />
  <figcaption>Raw trace log text — technically complete, but exhausting to read straight from the file.</figcaption>
</figure>

## What it does

- **Loads logs three ways** — from local files, remotely over **SSH/SFTP**, or re-opened from
  a local store.
- **Streams and parses** trace files of any size, off the UI thread, so large logs stay responsive.
- **Renders typed event cards** for every Firebird trace event type (attach/detach, statement
  start/restart/finish, failed statements, procedures, triggers, errors, and more).
- **Filters, sorts and searches** — multi-select, numeric ranges, date/time ranges, full-text
  search and stable multi-key sorting.
- **Optionally persists** parsed sessions in a local **SQLite** store (WAL) with deduplication,
  storage analytics, a read-only SQL console and on-demand compaction.
- **Builds and exports reports** — a live designer with grouping and aggregation, reusable
  templates, and export to **PDF, DOCX, XLSX and CSV**.
- **Extends** via drop-in sort/filter plugins.
- **Themes & languages** — Light / Dark / Auto, English and Russian.

## Who it's for

Firebird DBAs, backend developers and support engineers who need to understand what a database
was doing — slow statements, failing queries, connection churn, trigger/procedure activity — and
to hand a clean report to someone else.

## License

The project is **source-available** under the
[PolyForm Noncommercial License 1.0.0](https://github.com/deliciousNesquik/firebird-trace-analyzer/blob/master/LICENSE.md):
free to use, modify and share for **noncommercial** purposes. For commercial use, contact the author.
