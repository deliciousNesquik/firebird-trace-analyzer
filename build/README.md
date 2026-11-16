# Сборка релизов

Скрипты собирают self-contained релизы (рантайм .NET внутри, отдельная установка не нужна) для macOS, Windows и Linux.

## Требования

- .NET 10 SDK (`dotnet --version` ≥ 10). Проект таргетит `net10.0`.
- macOS-бандл и `icon.icns` полноценны только при сборке на macOS: нужны системные `sips`, `iconutil`, `codesign`, `ditto`.
- `zip` — для Windows-архива. В macOS и на обычных Linux он есть; на минимальных образах может отсутствовать, и тогда сборка win упадёт на шаге упаковки.

Кросс-публикация работает с любого хоста: с macOS собираются и win, и linux. Полноценный `.app` с иконкой и подписью получается только на macOS.

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

## Нюансы сборки

- Подпись — ад-хок `codesign`. Этого достаточно для запуска на машине сборки: неподписанный бандл на Apple Silicon убивает Gatekeeper. Для распространения другим пользователям нужны Developer ID и нотаризация (`xcrun notarytool`), это уже за рамками скрипта.
- Под linux рантайм встроен, но Avalonia требует системные библиотеки: `libicu`, `fontconfig`, `libfreetype`, шрифты, X11/Wayland. На «голом» сервере без GUI приложение не стартует.
- Trimming не включён намеренно. Приложение использует рефлексию: `FieldDiscoveryService` сканирует свойства моделей событий на `[SortableField]`/`[FilterableField]`, плагины загружаются через `PluginLoadContext`. Обрезка сломала бы рантайм.
- Каталог `Configuration/` копируется в output целиком (так настроен csproj): приложению нужны `rules.json`, `rules.schema.json` и `nlog.config` рядом с исполняемым файлом. Всё, что окажется в исходном каталоге, уедет в релиз, поэтому лишних файлов там быть не должно.
