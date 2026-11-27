# Release builds

The scripts produce **self-contained** releases (the .NET runtime is bundled, no separate
installation needed) for macOS, Windows and Linux.

## Requirements

- .NET 10 SDK (`dotnet --version` ≥ 10). The project targets `net10.0`.
- The macOS bundle and `icon.icns` are only fully built on macOS: the system tools `sips`,
  `iconutil`, `codesign` and `ditto` are required.
- `zip` — for the Windows archive. It ships with macOS and most Linux systems; on minimal
  images it may be missing, in which case the win build fails at the packaging step.

Cross-publishing works from any host: both win and linux can be built from macOS. A complete
`.app` with icon and signature only comes out of a macOS host.

## Usage

```bash
# everything at once (mac for the current architecture + win-x64 + linux-x64)
./build/build.sh

# one platform at a time
./build/build.sh mac
./build/build.sh win
./build/build.sh linux

# a specific version and/or mac architecture
./build/build.sh mac 1.2.0
MAC_RID=osx-x64 ./build/build.sh mac      # Intel build on Apple Silicon

# rebuild the icon only
./build/make-icns.sh
```

Artifacts land in `build/artifacts/`:

| Platform | Result |
|----------|--------|
| macOS | `FirebirdTraceAnalyzer.app` + `FirebirdTraceAnalyzer-<ver>-macos-<arch>.zip` |
| Windows | `FirebirdTraceAnalyzer-<ver>-win-x64.zip` |
| Linux | `FirebirdTraceAnalyzer-<ver>-linux-x64.tar.gz` |

## macOS .app structure

```
FirebirdTraceAnalyzer.app/
└── Contents/
    ├── Info.plist                 # bundle metadata (version, icon, bundle id)
    ├── MacOS/
    │   ├── FirebirdTraceAnalyzer  # executable (CFBundleExecutable)
    │   └── …                      # native libraries from publish (Skia etc.)
    └── Resources/
        └── icon.icns              # icon (CFBundleIconFile = icon)
```

`icon.icns` is generated from `FirebirdTraceAnalyzer/Assets/app-logo.svg` (rasterized via
`sips`, fallback: `app-logo.ico`).

## Release notes sync

`sync-release-notes.py` copies release notes from the docs site into GitHub Releases: each
`## <version> — <date>` section of `docs/src/content/docs/changes/whats-new.mdx` (EN — the
source of truth; the RU mirror stays site-only) becomes the body of the release with the
matching tag. The heading itself is not copied — the release title already carries the tag.
Releases without a matching section are left alone (`v1.0.0-alpha`).

```bash
python3 build/sync-release-notes.py --dry-run   # show diffs
python3 build/sync-release-notes.py             # PATCH the changed ones
```

Run it after editing `whats-new.mdx` and pushing — GitHub does not do this automatically.
Token resolution: `--token` > `GITHUB_TOKEN` env > `~/.config/github-token` > `gh auth token`.

## Build caveats

- The signature is ad-hoc `codesign`. That is enough to run on the build machine: without a
  signature, Gatekeeper kills the bundle on Apple Silicon. Distributing to other users requires
  a Developer ID signature and notarization (`xcrun notarytool`) — beyond the script's scope.
- On Linux the runtime is embedded, but Avalonia needs system libraries: `libicu`,
  `fontconfig`, `libfreetype`, fonts, X11/Wayland. The app won't start on a bare headless
  server.
- Trimming is off on purpose. The app relies on reflection: `FieldDiscoveryService` scans event
  model properties for `[SortableField]`/`[FilterableField]`, and plugins are loaded through
  `PluginLoadContext`. Trimming would break the runtime.
- The `Configuration/` directory is copied to the output as a whole (that's how the csproj is
  set up): the app needs `rules.json`, `rules.schema.json` and `nlog.config` next to the
  executable. Anything that ends up in the source directory ships with the release, so it
  should contain no stray files.
