#!/usr/bin/env python3
"""Sync GitHub Release bodies from the EN whats-new page.

For every release whose tag has a matching section in
docs/src/content/docs/changes/whats-new.mdx, replaces the release body with the
section's markdown. The EN page is the source of truth for release bodies; the RU
mirror (docs/src/content/docs/ru/changes/whats-new.mdx) stays site-only.

Section headings look like `## 1.17.0 — November 27, 2026`; the heading itself is
NOT copied into the body (the release title already carries the tag). Releases
without a matching section are skipped (e.g. v1.0.0-alpha).

Usage:
    python3 build/sync-release-notes.py            # apply diffs
    python3 build/sync-release-notes.py --dry-run  # show what would change

Token resolution: --token argument > GITHUB_TOKEN env > `gh auth token`.
Requires: python3 (stdlib only) and curl.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

DEFAULT_REPO = "deliciousNesquik/firebird-trace-analyzer"
SECTION_RE = re.compile(r"^## (\d+\.\d+\.\d+) — .+$", re.M)


def parse_sections(markdown: str) -> dict[str, str]:
    """{version: body} from `## <semver> — <date>` headings; heading excluded."""
    matches = list(SECTION_RE.finditer(markdown))
    sections: dict[str, str] = {}
    for i, m in enumerate(matches):
        end = matches[i + 1].start() if i + 1 < len(matches) else len(markdown)
        sections[m.group(1)] = markdown[m.end():end].strip()
    return sections


def resolve_token(cli_token: str | None) -> str:
    if cli_token:
        return cli_token
    if env := os.environ.get("GITHUB_TOKEN"):
        return env
    # gh CLI хранит отдельный токен; наш канал — env GITHUB_TOKEN или файл.
    token_file = Path.home() / ".config" / "github-token"
    if token_file.exists():
        return token_file.read_text(encoding="utf-8").strip()
    try:
        out = subprocess.run(["gh", "auth", "token"], capture_output=True, text=True)
        if out.returncode == 0 and out.stdout.strip():
            return out.stdout.strip()
    except FileNotFoundError:
        pass
    sys.exit("No token: pass --token, set GITHUB_TOKEN, or create ~/.config/github-token")


class GitHub:
    def __init__(self, token: str) -> None:
        self._header_file = tempfile.NamedTemporaryFile(
            "w", suffix=".hdr", delete=False
        )
        self._header_file.write(f"Authorization: Bearer {token}\n")
        self._header_file.write("Accept: application/vnd.github+json\n")
        self._header_file.close()
        os.chmod(self._header_file.name, 0o600)

    def __enter__(self) -> "GitHub":
        return self

    def __exit__(self, *exc: object) -> None:
        os.unlink(self._header_file.name)

    def _curl(self, url: str, method: str = "GET", data: dict | None = None) -> dict:
        cmd = ["curl", "-sS", "-X", method, "-H", f"@{self._header_file.name}"]
        if data is not None:
            cmd += ["-H", "Content-Type: application/json", "-d", json.dumps(data)]
        cmd.append(url)
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
        return json.loads(result.stdout) if result.stdout else {}

    def releases(self, repo: str) -> list[dict]:
        url = f"https://api.github.com/repos/{repo}/releases?per_page=100"
        return self._curl(url)

    def update_body(self, repo: str, release_id: int, body: str) -> None:
        url = f"https://api.github.com/repos/{repo}/releases/{release_id}"
        self._curl(url, "PATCH", {"body": body})


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--repo", default=DEFAULT_REPO)
    parser.add_argument("--file", type=Path, default=None,
                        help="path to whats-new.mdx (default: repo-relative)")
    parser.add_argument("--token", default=None)
    parser.add_argument("--dry-run", action="store_true",
                        help="show diffs without PATCHing")
    args = parser.parse_args()

    md_path = args.file
    if md_path is None:
        md_path = Path(__file__).resolve().parent.parent / (
            "docs/src/content/docs/changes/whats-new.mdx"
        )
    sections = parse_sections(md_path.read_text(encoding="utf-8"))
    if not sections:
        sys.exit(f"No version sections found in {md_path}")

    with GitHub(resolve_token(args.token)) as gh:
        releases = gh.releases(args.repo)
        changed = unchanged = 0
        skipped: list[str] = []
        for rel in releases:
            tag, current = rel["tag_name"], rel.get("body") or ""
            desired = sections.get(tag.lstrip("v"))
            if desired is None:
                skipped.append(tag)
                continue
            if current.strip() == desired:
                unchanged += 1
                continue
            changed += 1
            print(f"{tag}: {len(current)} -> {len(desired)} chars")
            if not args.dry_run:
                gh.update_body(args.repo, rel["id"], desired)

        print(f"\n{'DRY RUN — ' if args.dry_run else ''}updated: {changed}, "
              f"already in sync: {unchanged}, skipped: {skipped or 'none'}")


if __name__ == "__main__":
    main()
