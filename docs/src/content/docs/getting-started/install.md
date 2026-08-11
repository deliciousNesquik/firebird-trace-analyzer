---
title: Install & run
description: Download a prebuilt release or build Firebird Trace Analyzer from source.
---

## Download a release

Prebuilt builds for Windows, macOS and Linux are published on the
[**Releases**](https://github.com/deliciousNesquik/firebird-trace-analyzer/releases) page.
Download the archive for your platform, unpack it and run the app — no installation required.

## Build from source

### Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download).

### Clone, run, build

```bash
git clone https://github.com/deliciousNesquik/firebird-trace-analyzer.git
cd firebird-trace-analyzer

# run the app
dotnet run --project FirebirdTraceAnalyzer

# or produce a Release build
dotnet build FirebirdTrace.sln -c Release
```

### Run the tests

```bash
dotnet test FirebirdTrace.sln
```

## First launch

On the first run the app creates its configuration and data folders under your user profile
(for example, settings, the optional event store and downloaded remote files). Nothing is
written outside your user profile unless you explicitly choose an export location.

Head to [**Loading logs**](/guides/loading-logs/) to open your first trace file.
