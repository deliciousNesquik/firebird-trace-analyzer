#!/usr/bin/env bash
#
# Сборка self-contained релизов FirebirdTraceAnalyzer для macOS / Windows / Linux.
#
# Использование:
#   build/build.sh [target] [version]
#     target  — all (по умолчанию) | mac | win | linux
#     version — переопределить версию (по умолчанию берётся <Version> из csproj)
#
# Переменные окружения:
#   MAC_RID — osx-arm64 | osx-x64 (по умолчанию = архитектура хоста)
#
# Требуется .NET 10 SDK. Кросс-публикация работает с любого хоста,
# но .app-бандл и icon.icns полноценно собираются только на macOS.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/FirebirdTraceAnalyzer/FirebirdTraceAnalyzer.csproj"
OUT="$ROOT/build/artifacts"
APP_NAME="FirebirdTraceAnalyzer"
DISPLAY_NAME="Firebird Trace Analyzer"
BUNDLE_ID="com.reid.firebirdtraceanalyzer"

TARGET="${1:-all}"

command -v dotnet >/dev/null || { echo "ERROR: не найден dotnet в PATH." >&2; exit 1; }

# Версия: аргумент → <Version> из csproj → 1.0.0
VERSION="${2:-}"
if [[ -z "$VERSION" ]]; then
    VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -1)"
    VERSION="${VERSION:-1.0.0}"
fi

echo "=== FirebirdTraceAnalyzer build — версия $VERSION, цель '$TARGET' ==="

# ── Публикация одного RID в каталог ───────────────────────────────────────────
publish() {
    local rid="$1" outdir="$2"
    echo "→ dotnet publish ($rid)…"
    rm -rf "$outdir"
    dotnet publish "$PROJECT" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:Version="$VERSION" \
        -p:InformationalVersion="$VERSION" \
        -o "$outdir"
    rm -f "$outdir"/*.pdb 2>/dev/null || true   # символы в релиз не кладём
}

# ── Linux ─────────────────────────────────────────────────────────────────────
build_linux() {
    local rid="linux-x64"
    local stage="$OUT/$rid"
    publish "$rid" "$stage"
    chmod +x "$stage/$APP_NAME" 2>/dev/null || true
    ( cd "$stage" && tar -czf "$OUT/${APP_NAME}-${VERSION}-linux-x64.tar.gz" . )
    echo "✓ Linux: $OUT/${APP_NAME}-${VERSION}-linux-x64.tar.gz"
}

# ── Windows ───────────────────────────────────────────────────────────────────
build_win() {
    local rid="win-x64"
    local stage="$OUT/$rid"
    publish "$rid" "$stage"
    ( cd "$stage" && zip -q -r "$OUT/${APP_NAME}-${VERSION}-win-x64.zip" . )
    echo "✓ Windows: $OUT/${APP_NAME}-${VERSION}-win-x64.zip"
}

# ── macOS (.app-бандл по канонам Apple) ───────────────────────────────────────
build_mac() {
    local rid="${MAC_RID:-}"
    if [[ -z "$rid" ]]; then
        case "$(uname -m)" in
            arm64) rid="osx-arm64" ;;
            *)     rid="osx-x64" ;;
        esac
    fi

    local stage="$OUT/$rid"
    publish "$rid" "$stage"

    # icon.icns (только на macOS)
    local icns="$ROOT/build/icon.icns"
    if [[ "$(uname)" == "Darwin" ]]; then
        "$ROOT/build/make-icns.sh" "" "$icns"
    else
        echo "⚠ Сборка не на macOS — пропускаю icon.icns (.app будет без иконки)."
    fi

    # Структура бандла:
    #   FirebirdTraceAnalyzer.app/
    #     Contents/Info.plist
    #     Contents/MacOS/<всё из publish, включая исполняемый файл и нативные либы>
    #     Contents/Resources/icon.icns
    local app="$OUT/${APP_NAME}.app"
    echo "→ Сборка бандла $app"
    rm -rf "$app"
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

    cp -R "$stage"/. "$app/Contents/MacOS/"
    [[ -f "$icns" ]] && cp "$icns" "$app/Contents/Resources/icon.icns"
    chmod +x "$app/Contents/MacOS/$APP_NAME"

    cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>                <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>         <string>${DISPLAY_NAME}</string>
    <key>CFBundleIdentifier</key>          <string>${BUNDLE_ID}</string>
    <key>CFBundleVersion</key>             <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key>  <string>${VERSION}</string>
    <key>CFBundlePackageType</key>         <string>APPL</string>
    <key>CFBundleExecutable</key>          <string>${APP_NAME}</string>
    <key>CFBundleIconFile</key>            <string>icon</string>
    <key>LSMinimumSystemVersion</key>      <string>11.0</string>
    <key>NSHighResolutionCapable</key>     <true/>
    <key>LSApplicationCategoryType</key>   <string>public.app-category.developer-tools</string>
</dict>
</plist>
PLIST

    # Ад-хок подпись: иначе Gatekeeper на Apple Silicon убивает неподписанный бандл.
    # Это НЕ нотаризация — для распространения вне своей машины нужна Developer ID + notarytool.
    if [[ "$(uname)" == "Darwin" ]] && command -v codesign >/dev/null; then
        if codesign --force --deep --sign - "$app" >/dev/null 2>&1; then
            echo "✓ ад-хок codesign"
        else
            echo "⚠ codesign не удался (не критично для локального запуска)."
        fi
    fi

    # Архив для распространения: ditto сохраняет права/симлинки/ресурс-форки.
    if [[ "$(uname)" == "Darwin" ]] && command -v ditto >/dev/null; then
        ditto -c -k --keepParent "$app" "$OUT/${APP_NAME}-${VERSION}-macos-${rid#osx-}.zip"
        echo "✓ macOS zip: $OUT/${APP_NAME}-${VERSION}-macos-${rid#osx-}.zip"
    fi
    echo "✓ macOS .app: $app"
}

mkdir -p "$OUT"
case "$TARGET" in
    mac)   build_mac ;;
    win)   build_win ;;
    linux) build_linux ;;
    all)   build_mac; build_win; build_linux ;;
    *) echo "ERROR: неизвестная цель '$TARGET' (ожидалось all|mac|win|linux)." >&2; exit 1 ;;
esac

echo "=== Готово. Артефакты: $OUT ==="
