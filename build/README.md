# Сборка релизов

Скрипты собирают **self-contained** релизы (рантайм .NET внутри, отдельная установка не нужна) для macOS, Windows и Linux.

## Требования

- **.NET 10 SDK** (`dotnet --version` ≥ 10). Проект таргетит `net10.0`.
- Для macOS-бандла и `icon.icns` — собирать **на macOS** (нужны `sips`, `iconutil`, `codesign`, `ditto`; все системные).
- `zip` — для Windows-архива (есть в macOS/Linux из коробки).

Кросс-публикация работает с любого хоста: с macOS можно собрать и win, и linux. Но полноценный `.app` с иконкой и подписью получается только на macOS.

## Использование

```bash
# всё сразу (mac под текущую архитектуру + win-x64 + linux-x64)
./build/build.sh

# по отдельности
./build/build.sh mac
./build/build.sh win
./build/build.sh linux

# конкретная версия и/или архитектура mac
./build/build.sh mac 1.2.0
MAC_RID=osx-x64 ./build/build.sh mac      # Intel-сборка на Apple Silicon

# только пересобрать иконку
./build/make-icns.sh
```

Артефакты складываются в `build/artifacts/`:

| Платформа | Результат |
|-----------|-----------|
| macOS | `FirebirdTraceAnalyzer.app` + `FirebirdTraceAnalyzer-<ver>-macos-<arch>.zip` |
| Windows | `FirebirdTraceAnalyzer-<ver>-win-x64.zip` |
| Linux | `FirebirdTraceAnalyzer-<ver>-linux-x64.tar.gz` |

## Структура macOS .app

```
FirebirdTraceAnalyzer.app/
└── Contents/
    ├── Info.plist                 # метаданные бандла (версия, иконка, bundle id)
    ├── MacOS/
    │   ├── FirebirdTraceAnalyzer  # исполняемый файл (CFBundleExecutable)
    │   └── …                      # нативные библиотеки из publish (Skia и т.п.)
    └── Resources/
        └── icon.icns              # иконка (CFBundleIconFile = icon)
```

`icon.icns` генерируется из `FirebirdTraceAnalyzer/Assets/app-logo.svg` (растеризация через `sips`, fallback — `app-logo.ico`).

## Важные замечания

- **Подпись.** Скрипт делает **ад-хок** `codesign` — этого достаточно, чтобы приложение запускалось на машине сборки (Gatekeeper иначе убивает неподписанный бандл на Apple Silicon). Для распространения другим пользователям нужна подпись **Developer ID** и **нотаризация** (`xcrun notarytool`) — это вне рамок скрипта.
- **Linux: системные зависимости.** Рантайм встроен, но Avalonia использует системные библиотеки: нужны `libicu`, `fontconfig`, `libfreetype`, шрифты, X11/Wayland. На «голом» сервере без GUI приложение не стартует.
- **Trimming НЕ включён** намеренно: приложение использует рефлексию (динамические поля сортировки/фильтров, RazorLight) — обрезка сломала бы рантайм.
- **`Configuration/*.log`** копируются в output (так настроен csproj). Это рантайм-логи NLog; при желании чистите каталог `Configuration/` от `*.log` перед сборкой.
