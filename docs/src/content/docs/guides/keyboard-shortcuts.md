---
title: Keyboard shortcuts
description: Every keyboard shortcut in Firebird Trace Analyzer (defaults for Windows/Linux and macOS) and how to rebind them.
---

Everything below is the **default** layout. Every action except opening Settings can be rebound in
**Settings → Keyboard shortcuts** — see [Customizing shortcuts](#customizing-shortcuts).
Windows and Linux use `Ctrl`; macOS uses `⌘` (Command).

## Files

| Action | Windows / Linux | macOS |
| --- | --- | --- |
| Open local file | `Ctrl+O` | `⌘O` |
| Open remote file | `Ctrl+Shift+O` | `⌘⇧O` |
| Cancel loading | `Ctrl+Esc` | `⌘Esc` |
| Reparse all files | `Ctrl+R` | `⌘R` |
| Reparse selected files | `Ctrl+Shift+R` | `⌘⇧R` |
| Close selected files | `Ctrl+W` | `⌘W` |
| Close all files | `Ctrl+Shift+W` | `⌘⇧W` |

## Layout

Each shortcut toggles one section on or off. Reset restores the default layout.

| Action | Windows / Linux | macOS |
| --- | --- | --- |
| Toggle Files | `Ctrl+1` | `⌘1` |
| Toggle Search | `Ctrl+2` | `⌘2` |
| Toggle Events | `Ctrl+3` | `⌘3` |
| Toggle Statistics | `Ctrl+4` | `⌘4` |
| Toggle Logs | `Ctrl+5` | `⌘5` |
| Reset layout | `Ctrl+0` | `⌘0` |

## Search

| Action | Windows / Linux | macOS |
| --- | --- | --- |
| Focus the search box | `Ctrl+F` | `⌘F` |
| Run search | `Enter` | `Enter` |

## Application

| Action | Windows / Linux | macOS |
| --- | --- | --- |
| Open Settings | `Ctrl+,` | `⌘,` |

## Reports

Report shortcuts have **no defaults** — each saved report template appears in
**Settings → Keyboard shortcuts**, and you can assign a combination to run it with one key press.

## Customizing shortcuts

Open **Settings → Keyboard shortcuts** (`Ctrl+,` / `⌘,`). Actions are grouped by category
(Files, Layout, Search, Application, Reports); each row shows the current combination and its
buttons.

### Rebind an action

1. Click **Set** on the row.
2. Press the new combination — it must include a modifier (`Ctrl`/`Alt`/`Shift`); bare letters are
   left for typing. **Esc** cancels.
3. Confirm with **Enter**. The change applies **immediately** — including the native macOS menu
   bar — and persists across restarts.

### Conflicts

If the combination is already assigned, the row shows **Already assigned to: …**. Click
**Take over** to move the shortcut — the previous owner is left without one. The **Open Settings**
shortcut is a system shortcut: it cannot be taken over or changed.

### Disable and reset

- **Disable** removes the shortcut from an action; the action stays reachable from the menu and
  shows `—` instead of a combination.
- **Back to default** (per row) restores the default combination.
- **Reset all shortcuts** deletes `shortcuts.json` — every action returns to its default.

### Where overrides live

Only **overrides** are stored: a missing entry means the default applies, an explicit `null`
means the action is disabled. The file is created on the first change and re-read at the next
launch.

| OS | Path |
| --- | --- |
| Windows | `%AppData%\FirebirdTraceAnalyzer\shortcuts.json` |
| macOS | `~/Library/Application Support/FirebirdTraceAnalyzer/shortcuts.json` |
| Linux | `~/.config/FirebirdTraceAnalyzer/shortcuts.json` |

Hand-editing is welcome — validate against the
[JSON schema](https://deliciousnesquik.github.io/firebird-trace-analyzer/shortcuts.schema.json).
Gestures are written as modifiers and a key joined with `+`, where `Primary` resolves to `Ctrl`
(Windows/Linux) or `⌘` (macOS): `"Primary+Shift+O"`, `"Ctrl+OemComma"`, `"Primary+F5"`. A corrupt
file or an unsupported `version` is backed up to `shortcuts.json.bak` and defaults apply.
Deleting the file resets everything.
