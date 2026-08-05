#!/usr/bin/env python3
"""Inspect and record block hashes for Git-shared OpenSpec display translations."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

LANGUAGES = {"zh-CN", "en-US"}
HUMAN_JSON_FIELDS = {
    "title", "summary", "details", "requirement", "impact", "recommendation",
    "userRationale", "deliveryBoundary", "implementationImpact", "acceptanceNote",
    "implementationNotes", "feature", "reason", "verificationSummary", "description",
}


def sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def project_root() -> Path:
    current = Path.cwd().resolve()
    for candidate in (current, *current.parents):
        if (candidate / "openspec").is_dir():
            return candidate
    raise SystemExit("No openspec directory found from the current path.")


def detect_language(text: str) -> str:
    cjk = len(re.findall(r"[\u3400-\u9fff]", text))
    latin = len(re.findall(r"\b[A-Za-z]{2,}\b", text))
    return "zh-CN" if cjk >= 2 and cjk * 5 >= latin else "en-US"


def authority_language(path: Path, text: str) -> str:
    if path.suffix.lower() == ".json":
        siblings = [path.parent / name for name in ("spec.md", "proposal.md", "design.md", "tasks.md")]
        markdown = [read_text(candidate) for candidate in siblings if candidate.exists()]
        if markdown:
            text = "\n".join(markdown)
    return detect_language(text)


def markdown_blocks(text: str) -> list[dict[str, str]]:
    lines = text.splitlines(keepends=True)
    blocks: list[tuple[str, str]] = []
    buffer: list[str] = []
    kind = "body"
    in_fence = False

    def flush() -> None:
        nonlocal buffer
        if buffer:
            blocks.append((kind, "".join(buffer)))
            buffer = []

    for line in lines:
        stripped = line.strip()
        if stripped.startswith("```"):
            if not in_fence:
                flush()
                kind = "code"
                in_fence = True
            buffer.append(line)
            if stripped != "```" and stripped.count("```") > 1:
                in_fence = False
                flush()
                kind = "body"
            elif stripped == "```" and len(buffer) > 1:
                in_fence = False
                flush()
                kind = "body"
            continue
        if in_fence:
            buffer.append(line)
            continue
        next_kind = (
            "heading" if re.match(r"^#{1,6}\s", stripped)
            else "task" if re.match(r"^[-*]\s+\[[ xX]\]\s", stripped)
            else "table" if stripped.startswith("|")
            else "list" if re.match(r"^(?:[-*+] |\d+[.)] )", stripped)
            else "blank" if not stripped
            else "body"
        )
        if next_kind in {"heading", "task"}:
            flush()
            kind = next_kind
            buffer.append(line)
            flush()
            kind = "body"
        elif next_kind != kind or next_kind == "blank":
            flush()
            kind = next_kind
            buffer.append(line)
            if next_kind == "blank":
                flush()
                kind = "body"
        else:
            buffer.append(line)
    flush()
    result = [
        {"id": f"md:{index:04d}", "kind": block_kind, "text": value}
        for index, (block_kind, value) in enumerate(blocks, 1)
    ]
    while result and result[-1]["kind"] == "blank":
        result.pop()
    return result


def json_blocks(text: str) -> list[dict[str, str]]:
    data = json.loads(text)
    result: list[dict[str, str]] = []

    def walk(value, pointer: str = "") -> None:
        if isinstance(value, dict):
            for key, child in value.items():
                child_pointer = f"{pointer}/{key}"
                if key in HUMAN_JSON_FIELDS and isinstance(child, str) and child.strip():
                    result.append({"id": "json:" + child_pointer, "kind": "json-text", "text": child})
                elif key in {"inspectorReferences", "tunableParameters", "sceneSetup", "usage", "differences"} and isinstance(child, list):
                    for index, item in enumerate(child):
                        if isinstance(item, str) and item.strip():
                            result.append({"id": f"json:{child_pointer}/{index}", "kind": "json-text", "text": item})
                else:
                    walk(child, child_pointer)
        elif isinstance(value, list):
            for index, child in enumerate(value):
                walk(child, f"{pointer}/{index}")

    walk(data)
    return result


def blocks_for(path: Path, text: str) -> list[dict[str, str]]:
    return json_blocks(text) if path.suffix.lower() == ".json" else markdown_blocks(text)


def manifest_path(root: Path) -> Path:
    return root / "openspec" / "translations" / "manifest.json"


def load_manifest(root: Path) -> dict:
    path = manifest_path(root)
    if not path.exists():
        return {"schemaVersion": 1, "entries": []}
    data = json.loads(read_text(path))
    data.setdefault("schemaVersion", 1)
    data.setdefault("entries", [])
    return data


def canonical_files(root: Path, scope: str | None) -> list[Path]:
    openspec = root / "openspec"
    allowed_names = {
        "proposal.md", "design.md", "tasks.md", "spec.md",
        "change-review.json", "spec-review.json", "gaps.json",
    }
    files = [p for p in openspec.rglob("*") if p.is_file() and p.name in allowed_names]
    files = [p for p in files if "translations" not in p.parts and "archive" not in p.parts]
    if scope:
        needle = scope.replace("\\", "/").strip("/").lower()
        files = [p for p in files if needle in p.relative_to(openspec).as_posix().lower()]
    return sorted(files)


def entry_key(entry: dict) -> tuple[str, str]:
    return entry.get("sourcePath", ""), entry.get("targetLanguage", "")


def changed_block_ids(
    source_blocks: list[dict[str, str]],
    target_blocks: list[dict[str, str]] | None,
    entry: dict | None,
) -> list[str]:
    """Return every source block that is not fully synchronized."""
    source_ids = [block["id"] for block in source_blocks]
    if not entry or target_blocks is None:
        return source_ids

    previous = {block.get("id"): block for block in entry.get("blocks", [])}
    target_by_id = {block["id"]: block for block in target_blocks}
    if any(block_id not in source_ids for block_id in target_by_id):
        return source_ids

    changed: list[str] = []
    for block in source_blocks:
        block_id = block["id"]
        recorded = previous.get(block_id)
        translated = target_by_id.get(block_id)
        if (
            not recorded
            or recorded.get("sourceHash") != sha256(block["text"])
            or not recorded.get("translatedHash")
            or not translated
            or translated.get("kind") != block.get("kind")
            or recorded.get("translatedHash") != sha256(translated["text"])
        ):
            changed.append(block_id)
    return changed


def translation_structure_issues(
    source_blocks: list[dict[str, str]],
    target_blocks: list[dict[str, str]],
) -> list[str]:
    """Describe structural omissions that would make a file-level translation partial."""
    issues: list[str] = []
    if len(source_blocks) != len(target_blocks):
        issues.append(
            f"block count differs (source={len(source_blocks)}, target={len(target_blocks)})"
        )
    for index, source_block in enumerate(source_blocks):
        if index >= len(target_blocks):
            issues.append(f"missing target block {source_block['id']}")
            continue
        target_block = target_blocks[index]
        if source_block["id"] != target_block["id"]:
            issues.append(
                f"block id differs at position {index + 1} "
                f"(source={source_block['id']}, target={target_block['id']})"
            )
        if source_block["kind"] != target_block["kind"]:
            issues.append(
                f"block kind differs for {source_block['id']} "
                f"(source={source_block['kind']}, target={target_block['kind']})"
            )
    if len(target_blocks) > len(source_blocks):
        issues.extend(
            f"unexpected target block {block['id']}"
            for block in target_blocks[len(source_blocks):]
        )
    return issues


def inspect(root: Path, language: str, scope: str | None) -> int:
    openspec = root / "openspec"
    manifest = load_manifest(root)
    entries = {entry_key(entry): entry for entry in manifest["entries"]}
    report = []
    for source in canonical_files(root, scope):
        source_rel = source.relative_to(openspec).as_posix()
        source_text = read_text(source)
        authority = authority_language(source, source_text)
        if authority == language:
            continue
        entry = entries.get((source_rel, language))
        target_rel = f"openspec/translations/{language}/{source_rel}"
        target = root / target_rel
        source_blocks = blocks_for(source, source_text)
        target_blocks = blocks_for(target, read_text(target)) if target.exists() else None
        changed = changed_block_ids(source_blocks, target_blocks, entry)
        state = "missing"
        if entry and target.exists():
            source_is_current = entry.get("sourceHash") == sha256(source_text)
            target_is_current = entry.get("translatedHash") == sha256(read_text(target))
            state = "current" if source_is_current and target_is_current and not changed else "stale"
        report.append({
            "sourcePath": source_rel,
            "targetPath": target_rel,
            "authoritativeLanguage": authority,
            "targetLanguage": language,
            "state": state,
            "changedBlocks": changed,
            "reusableBlocks": len(source_blocks) - len(changed),
        })
    print(json.dumps({"schemaVersion": 1, "items": report}, ensure_ascii=False, indent=2))
    return 0


def record(root: Path, language: str, source_arg: str, target_arg: str) -> int:
    openspec = root / "openspec"
    source = (root / source_arg).resolve() if source_arg.startswith("openspec/") else (openspec / source_arg).resolve()
    target = (root / target_arg).resolve()
    if not source.is_relative_to(openspec.resolve()) or not target.is_relative_to((openspec / "translations").resolve()):
        raise SystemExit("Source or target escapes its allowed OpenSpec directory.")
    source_text = read_text(source)
    target_text = read_text(target)
    source_blocks = blocks_for(source, source_text)
    target_blocks = blocks_for(target, target_text)
    structure_issues = translation_structure_issues(source_blocks, target_blocks)
    if structure_issues:
        details = "; ".join(structure_issues)
        raise SystemExit(
            "Translation target is incomplete for its authoritative file: " + details
        )
    target_by_id = {block["id"]: block for block in target_blocks}
    source_rel = source.relative_to(openspec).as_posix()
    target_rel = target.relative_to(root).as_posix()
    entry = {
        "sourcePath": source_rel,
        "targetPath": target_rel,
        "authoritativeLanguage": authority_language(source, source_text),
        "targetLanguage": language,
        "sourceHash": sha256(source_text),
        "translatedHash": sha256(target_text),
        "blocks": [
            {
                "id": block["id"],
                "kind": block["kind"],
                "sourceHash": sha256(block["text"]),
                "translatedHash": sha256(target_by_id[block["id"]]["text"]) if block["id"] in target_by_id else "",
            }
            for block in source_blocks
        ],
    }
    manifest = load_manifest(root)
    manifest["entries"] = [item for item in manifest["entries"] if entry_key(item) != entry_key(entry)]
    manifest["entries"].append(entry)
    manifest["entries"].sort(key=lambda item: entry_key(item))
    path = manifest_path(root)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(entry, ensure_ascii=False, indent=2))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    inspect_parser = subparsers.add_parser("inspect")
    inspect_parser.add_argument("--language", required=True, choices=sorted(LANGUAGES))
    inspect_parser.add_argument("--scope")
    record_parser = subparsers.add_parser("record")
    record_parser.add_argument("--language", required=True, choices=sorted(LANGUAGES))
    record_parser.add_argument("--source", required=True)
    record_parser.add_argument("--target", required=True)
    args = parser.parse_args()
    root = project_root()
    if args.command == "inspect":
        return inspect(root, args.language, args.scope)
    return record(root, args.language, args.source, args.target)


if __name__ == "__main__":
    sys.exit(main())
