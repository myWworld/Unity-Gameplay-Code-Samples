#!/usr/bin/env python3
"""Validate the hygiene of the public source snapshot using only stdlib."""

from __future__ import annotations

import hashlib
import re
import sys
from collections import defaultdict
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]

REQUIRED_PATHS = (
    "README.md",
    "README.en.md",
    "LICENSE",
    "NOTICE.md",
    "docs/ARCHITECTURE.md",
    "docs/DEPENDENCIES.md",
    "docs/PUBLIC_SNAPSHOT.md",
    "docs/REVIEW_GUIDE.md",
    "Samples/RuntimeBuildingSystem/README.md",
    "Samples/BehaviorTreeUtilityAI/README.md",
    "Samples/BossCombatFramework/README.md",
)

TEXT_SUFFIXES = {
    "",
    ".cs",
    ".md",
    ".py",
    ".yml",
    ".yaml",
    ".json",
    ".txt",
    ".gitignore",
    ".gitattributes",
    ".editorconfig",
}

FORBIDDEN_DIRECTORY_NAMES = {
    "Library",
    "Temp",
    "Obj",
    "Logs",
    "UserSettings",
    ".vs",
    ".idea",
    "__pycache__",
}

MARKDOWN_LINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")


def iter_text_files() -> list[Path]:
    files: list[Path] = []
    for path in ROOT.rglob("*"):
        if not path.is_file() or ".git" in path.parts:
            continue
        if path.suffix.lower() in TEXT_SUFFIXES or path.name in {
            "LICENSE",
            "NOTICE.md",
            ".gitignore",
            ".gitattributes",
            ".editorconfig",
        }:
            files.append(path)
    return files


def check_required_paths(errors: list[str]) -> None:
    for relative in REQUIRED_PATHS:
        if not (ROOT / relative).is_file():
            errors.append(f"Missing required file: {relative}")


def check_forbidden_directories(errors: list[str]) -> None:
    for path in ROOT.rglob("*"):
        if path.is_dir() and path.name in FORBIDDEN_DIRECTORY_NAMES:
            errors.append(f"Generated/private directory must not be committed: {path.relative_to(ROOT)}")


def check_utf8_and_text_hygiene(errors: list[str]) -> None:
    for path in iter_text_files():
        relative = path.relative_to(ROOT)
        try:
            content = path.read_text(encoding="utf-8")
        except UnicodeDecodeError as exc:
            errors.append(f"Not valid UTF-8: {relative} ({exc})")
            continue

        if "\ufffd" in content:
            errors.append(f"Unicode replacement character found: {relative}")
        if "\r" in content:
            errors.append(f"CR/CRLF line ending found; expected LF: {relative}")
        if content and not content.endswith("\n"):
            errors.append(f"Missing final newline: {relative}")


def check_duplicate_csharp(errors: list[str]) -> tuple[int, int]:
    by_hash: dict[str, list[Path]] = defaultdict(list)
    cs_files = sorted(ROOT.glob("Samples/**/*.cs"))
    total_lines = 0

    for path in cs_files:
        data = path.read_bytes()
        by_hash[hashlib.sha256(data).hexdigest()].append(path)
        total_lines += len(data.decode("utf-8").splitlines())

    for paths in by_hash.values():
        if len(paths) > 1:
            joined = ", ".join(str(path.relative_to(ROOT)) for path in paths)
            errors.append(f"Duplicate C# file contents: {joined}")

    return len(cs_files), total_lines


def normalize_link_target(raw_target: str) -> str:
    target = raw_target.strip()
    if target.startswith("<") and target.endswith(">"):
        target = target[1:-1]
    if " " in target and not target.startswith(("http://", "https://")):
        target = target.split(" ", 1)[0]
    return unquote(target)


def check_markdown_links(errors: list[str]) -> None:
    for markdown in ROOT.rglob("*.md"):
        content = markdown.read_text(encoding="utf-8")
        for match in MARKDOWN_LINK_RE.finditer(content):
            target = normalize_link_target(match.group(1))
            if not target or target.startswith(("#", "http://", "https://", "mailto:")):
                continue

            path_part = target.split("#", 1)[0]
            if not path_part:
                continue

            resolved = (markdown.parent / path_part).resolve()
            try:
                resolved.relative_to(ROOT)
            except ValueError:
                errors.append(
                    f"Markdown link escapes repository: {markdown.relative_to(ROOT)} -> {target}"
                )
                continue

            if not resolved.exists():
                errors.append(
                    f"Broken local Markdown link: {markdown.relative_to(ROOT)} -> {target}"
                )


def main() -> int:
    errors: list[str] = []

    check_required_paths(errors)
    check_forbidden_directories(errors)
    check_utf8_and_text_hygiene(errors)
    cs_count, total_lines = check_duplicate_csharp(errors)
    check_markdown_links(errors)

    if errors:
        print("Public snapshot validation failed:\n")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Public snapshot validation passed.")
    print(f"- C# source files: {cs_count}")
    print(f"- C# source lines: {total_lines}")
    print("- Required documents: present")
    print("- Text encoding/line endings: UTF-8 + LF")
    print("- Duplicate C# contents: none")
    print("- Local Markdown links: valid")
    return 0


if __name__ == "__main__":
    sys.exit(main())
