#!/usr/bin/env bash
#
# Генерирует macOS icon.icns из векторного логотипа приложения.
#
# Использование:
#   build/make-icns.sh [SOURCE] [OUTPUT.icns]
#     SOURCE      — путь к иконке-источнику (по умолчанию Assets/app-logo.svg,
#                   запасной вариант — app-logo.ico). sips растеризует и svg, и ico.
#     OUTPUT.icns — куда положить результат (по умолчанию build/icon.icns)
#
# Требуется macOS: используются системные утилиты sips и iconutil.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEFAULT_SVG="$ROOT/FirebirdTraceAnalyzer/Assets/app-logo.svg"
DEFAULT_ICO="$ROOT/FirebirdTraceAnalyzer/Assets/app-logo.ico"

SRC="${1:-}"
OUT="${2:-$ROOT/build/icon.icns}"

if [[ "$(uname)" != "Darwin" ]]; then
    echo "ERROR: make-icns.sh нужно запускать на macOS (используются sips/iconutil)." >&2
    exit 1
fi
command -v sips     >/dev/null || { echo "ERROR: не найден sips." >&2; exit 1; }
command -v iconutil >/dev/null || { echo "ERROR: не найден iconutil." >&2; exit 1; }

# Источник: аргумент → svg → ico
if [[ -z "$SRC" ]]; then
    if [[ -f "$DEFAULT_SVG" ]]; then SRC="$DEFAULT_SVG"
    elif [[ -f "$DEFAULT_ICO" ]]; then SRC="$DEFAULT_ICO"
    else echo "ERROR: не найден ни $DEFAULT_SVG, ни $DEFAULT_ICO." >&2; exit 1
    fi
fi
[[ -f "$SRC" ]] || { echo "ERROR: источник иконки не найден: $SRC" >&2; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
MASTER="$WORK/master.png"
ICONSET="$WORK/icon.iconset"
mkdir -p "$ICONSET"

echo "→ Источник: $SRC"

# Мастер 1024×1024. sips умеет растеризовать .svg/.ico через ImageIO.
if ! sips -s format png -z 1024 1024 "$SRC" --out "$MASTER" >/dev/null 2>&1; then
    echo "ERROR: не удалось растеризовать $SRC через sips." >&2
    exit 1
fi

# Канонический набор размеров для .icns
gen() { sips -z "$1" "$1" "$MASTER" --out "$ICONSET/$2" >/dev/null; }
gen 16   icon_16x16.png
gen 32   icon_16x16@2x.png
gen 32   icon_32x32.png
gen 64   icon_32x32@2x.png
gen 128  icon_128x128.png
gen 256  icon_128x128@2x.png
gen 256  icon_256x256.png
gen 512  icon_256x256@2x.png
gen 512  icon_512x512.png
gen 1024 icon_512x512@2x.png

mkdir -p "$(dirname "$OUT")"
iconutil -c icns "$ICONSET" -o "$OUT"
echo "✓ icon.icns создан: $OUT"
