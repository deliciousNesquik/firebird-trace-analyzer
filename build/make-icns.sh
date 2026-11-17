#!/usr/bin/env bash
#
# Generate the macOS icon.icns from the app logo.
#
# Usage: build/make-icns.sh [SOURCE] [OUTPUT.icns]
#   SOURCE      - icon source (default: Assets/app-logo.svg, fallback: app-logo.ico)
#   OUTPUT.icns - result path (default: build/icon.icns)
#
# Notes:
#   - macOS only: uses the system sips and iconutil.
#   - sips rasterizes both .svg and .ico via ImageIO.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEFAULT_SVG="$ROOT/FirebirdTraceAnalyzer/Assets/app-logo.svg"
DEFAULT_ICO="$ROOT/FirebirdTraceAnalyzer/Assets/app-logo.ico"

SRC="${1:-}"
OUT="${2:-$ROOT/build/icon.icns}"

if [[ "$(uname)" != "Darwin" ]]; then
    echo "ERROR: make-icns.sh must be run on macOS (uses sips/iconutil)." >&2
    exit 1
fi
command -v sips     >/dev/null || { echo "ERROR: sips not found." >&2; exit 1; }
command -v iconutil >/dev/null || { echo "ERROR: iconutil not found." >&2; exit 1; }

if [[ -z "$SRC" ]]; then
    if [[ -f "$DEFAULT_SVG" ]]; then SRC="$DEFAULT_SVG"
    elif [[ -f "$DEFAULT_ICO" ]]; then SRC="$DEFAULT_ICO"
    else echo "ERROR: neither $DEFAULT_SVG nor $DEFAULT_ICO found." >&2; exit 1
    fi
fi
[[ -f "$SRC" ]] || { echo "ERROR: icon source not found: $SRC" >&2; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
MASTER="$WORK/master.png"
ICONSET="$WORK/icon.iconset"
mkdir -p "$ICONSET"

echo "Source: $SRC"

if ! sips -s format png -z 1024 1024 "$SRC" --out "$MASTER" >/dev/null 2>&1; then
    echo "ERROR: failed to rasterize $SRC with sips." >&2
    exit 1
fi

# Канонический набор размеров/имён для .iconset (требование iconutil).
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
echo "[OK] icon.icns created: $OUT"
