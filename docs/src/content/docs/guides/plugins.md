---
title: Plugins
description: Extend the app with custom sort and filter plugins.
---

Firebird Trace Analyzer can be extended with **custom sort and filter plugins** — .NET assemblies
that are discovered and loaded from a dedicated plugins folder on startup.

## How it works

- Drop a plugin assembly into the plugins folder; it's picked up on the next launch.
- Plugins can be **enabled or disabled** individually. A disabled plugin's assembly isn't even
  loaded, so its code doesn't run.
- If two plugins declare the same id, the app keeps the highest version and **shadows** the
  others; you can resolve the collision by disabling the ones you don't want.

## Trust model

:::danger[Plugins run with full privileges]
A plugin is a .NET assembly that executes with the **full privileges of the application** — there
is no sandbox. Installing a plugin is equivalent to running any other program on your machine.
**Only install plugins from sources you trust.**
:::

## Managing plugins

Use the plugins screen to see everything that was discovered — with its name, author, version and
status (Active / Disabled / Shadowed / Load error) — and to enable or disable each one. Changes
take effect on the next launch.
