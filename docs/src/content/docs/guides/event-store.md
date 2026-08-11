---
title: Local event store
description: Keep parsed sessions in a local SQLite store, re-open them instantly and run ad-hoc analytics.
---

The event store is an **optional** local database that keeps the events you've parsed, so you can
re-open past sessions instantly instead of re-parsing the original files. It's a plain
**SQLite** database in WAL mode, stored under your user profile.

## Turning it on

The store is controlled by a **storage mode** in the app's settings. When it's off, nothing is
written to disk; when it's on, parsed sessions are persisted and reused on the next launch.

## What it stores (and how it stays small)

Events are written along with their metadata, but repeated **SQL text** and **connection
details** are **deduplicated** — the same statement text or attachment is stored once and
referenced, which keeps the database compact even across many sessions.

## Managing storage

Open **Storage → Storage Management** to:

- see per-store **statistics** (file count, event count, database size);
- **delete** individual sessions or **clear** everything;
- **export / import** the store to move it between machines;
- **Compact now** — reclaim disk space after deletions (a full rebuild + `VACUUM`).

:::note
Deleting sessions marks space as free inside the database but doesn't shrink the file on its own.
**Compaction** rebuilds the database and returns the freed space to the operating system; it also
runs automatically in the background on the next launch after deletions.
:::

## Ad-hoc SQL analytics

The storage analytics view includes a **read-only SQL console**: run your own `SELECT` queries
against the stored events for custom analysis. Queries run on a separate, read-only connection,
so heavy analytics never block writing new events, and non-`SELECT` statements are rejected.
