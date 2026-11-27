#!/usr/bin/env python3
"""Validate media in docs/public/video-tutorials against the recording standard.

Checks (see docs/public/video-tutorials/README.md):
  * naming:  fta-<action>.mp4|png, kebab-case, optional -dark suffix
  * dimensions:
      video (ffprobe): 1200x900 or exactly 2x on Retina (2400x1800)
      png (IHDR parse, stdlib only): same set
  * video codec/container: h264 in mp4 (audio stream = reject: standard is mute)

Exit code 0 = all files pass (or directory empty); 1 = at least one violation.
"""
from __future__ import annotations

import re
import struct
import subprocess
import sys
from pathlib import Path

MEDIA_DIR = Path(__file__).resolve().parent.parent / "docs" / "public" / "video-tutorials"
NAME_RE = re.compile(r"^fta-[a-z0-9]+(-[a-z0-9-]+)*(-dark)?\.(mp4|png)$")
ALLOWED_DIMS = {(1200, 900), (2400, 1800)}


def png_dimensions(path: Path) -> tuple[int, int] | None:
    """Width/height from PNG IHDR (stdlib only), or None if not a PNG."""
    try:
        with path.open("rb") as f:
            head = f.read(24)
        if head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
            return None
        width, height = struct.unpack(">II", head[16:24])
        return width, height
    except OSError:
        return None


def video_dimensions(path: Path) -> tuple[int, int] | None:
    try:
        out = subprocess.run(
            ["ffprobe", "-v", "error", "-select_streams", "v:0",
             "-show_entries", "stream=width,height,codec_name",
             "-of", "json", str(path)],
            capture_output=True, text=True, check=True,
        ).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return None
    import json
    streams = json.loads(out).get("streams", [])
    if not streams:
        return None
    s = streams[0]
    if s.get("codec_name") != "h264":
        return None
    return s.get("width"), s.get("height")


def has_audio(path: Path) -> bool:
    out = subprocess.run(
        ["ffprobe", "-v", "error", "-select_streams", "a",
         "-show_entries", "stream=index", "-of", "json", str(path)],
        capture_output=True, text=True, check=True,
    ).stdout
    import json
    return bool(json.loads(out).get("streams"))


def main() -> None:
    files = sorted(p for p in MEDIA_DIR.iterdir() if p.is_file() and p.suffix in (".mp4", ".png")) \
        if MEDIA_DIR.is_dir() else []
    if not files:
        print(f"{MEDIA_DIR}: нет медиа-файлов — проверять нечего")
        return

    failures = 0
    for p in files:
        problems: list[str] = []

        if not NAME_RE.match(p.name):
            problems.append(f"имя не по шаблону fta-<действие>[-dark].mp4|png: {p.name}")

        dims = (video_dimensions(p) if p.suffix == ".mp4" else png_dimensions(p))
        if dims is None:
            problems.append(f"не читается как {p.suffix[1:]} (или кодек не h264)")
        elif dims not in ALLOWED_DIMS:
            problems.append(f"размер {dims[0]}x{dims[1]} не из {{1200x900, 2400x1800}}")

        if p.suffix == ".mp4" and has_audio(p):
            problems.append("есть звуковая дорожка (стандарт — без звука)")

        status = "OK " if not problems else "FAIL"
        print(f"{status} {p.name}: {dims[0]}x{dims[1]}" if dims and not problems else f"{status} {p.name}")
        for problem in problems:
            print(f"     — {problem}")
        failures += bool(problems)

    print(f"\nитого: {len(files) - failures} ок, {failures} нарушений")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
