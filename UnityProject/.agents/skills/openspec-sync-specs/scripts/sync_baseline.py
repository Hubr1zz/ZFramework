#!/usr/bin/env python3
"""Capture and validate optimistic-concurrency baselines for OpenSpec delta sync."""

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def relative(root, path):
    return path.resolve().relative_to(root.resolve()).as_posix()


def timestamp():
    return datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")


REQUIREMENT_RE = re.compile(
    r"^### Requirement:\s*(?P<name>.+?)\s*$\n(?P<body>.*?)(?=^### Requirement:|^## (?:ADDED|MODIFIED|REMOVED|RENAMED) Requirements|\Z)",
    re.MULTILINE | re.DOTALL,
)


def requirement_blocks(path):
    if not path.exists():
        return {}
    content = path.read_text(encoding="utf-8-sig")
    return {
        match.group("name").strip(): re.sub(r"\s+", " ", match.group(0)).strip()
        for match in REQUIREMENT_RE.finditer(content)
    }


def changed_requirement_names(base_path, current_path):
    base, current = requirement_blocks(base_path), requirement_blocks(current_path)
    return {name for name in set(base) | set(current) if base.get(name) != current.get(name)}


def concurrent_change(item, kind, message, assessment, **extra):
    result = {"type": kind, "capability": item.get("capability", ""), "message": message, "assessment": assessment}
    result.update(extra)
    return result


def targets(root, change):
    result = []
    for delta in sorted((change / "specs").glob("*/spec.md")):
        review_path = delta.parent / "spec-review.json"
        review = read(review_path) if review_path.exists() else {}
        capability = review.get("capability") or delta.parent.name
        target = root / "openspec" / "specs" / capability / "spec.md"
        snapshot = change / ".sync-baseline" / capability / "spec.md"
        if target.exists():
            snapshot.parent.mkdir(parents=True, exist_ok=True)
            snapshot.write_bytes(target.read_bytes())
        result.append({
            "capability": capability,
            "title": review.get("title") or capability,
            "category": review.get("category") or "unclassified",
            "deltaSpecPath": relative(root, delta),
            "targetSpecPath": relative(root, target),
            "targetExisted": target.exists(),
            "baseFileHash": digest(target) if target.exists() else "",
            "baseSnapshotPath": relative(root, snapshot) if target.exists() else "",
        })
    return result


def capture(args):
    root, change = Path(args.project_root).resolve(), Path(args.change_root).resolve()
    review_path = change / "change-review.json"
    review = read(review_path)
    if review.get("syncTargets") and not args.force:
        raise SystemExit("syncTargets already exist; --force requires an explicit rebase")
    review["syncTargets"] = targets(root, change)
    review["specSyncStatus"] = "pending"
    review["syncValidation"] = {"status": "baseline-captured", "summary": "正式 Spec 基线已记录。", "conflicts": [], "validatedAt": timestamp()}
    write(review_path, review)
    print(json.dumps(review["syncValidation"], ensure_ascii=False, indent=2))
    return 0


def validate(args):
    root, change = Path(args.project_root).resolve(), Path(args.change_root).resolve()
    review_path = change / "change-review.json"
    review = read(review_path)
    changes, review_required = [], False
    items = review.get("syncTargets") or []
    if not items:
        changes.append({"type": "missing-baseline", "capability": "", "message": "缺少正式 Spec 基线，请先显式 rebase。", "assessment": "review-required"})
        review_required = True
    for item in items:
        path = root / item["targetSpecPath"]
        delta = root / item["deltaSpecPath"]
        snapshot_value = item.get("baseSnapshotPath", "")
        snapshot = root / snapshot_value if snapshot_value else None
        existed = bool(item.get("targetExisted"))
        if existed and not path.exists():
            changes.append(concurrent_change(item, "target-removed", f"正式 Spec 已被删除：{item['targetSpecPath']}；需要判断重建是否会恢复已删除功能。", "review-required"))
            review_required = True
        elif not existed and path.exists():
            overlap = sorted(set(requirement_blocks(path)) & set(requirement_blocks(delta)))
            assessment = "merge-safe" if not overlap else "review-required"
            message = (f"正式 Spec 后来被创建，但与 Delta 的 Requirement 不重叠，可保留双方内容：{item['targetSpecPath']}" if not overlap
                       else f"正式 Spec 后来被创建，且与 Delta 触及相同 Requirement：{', '.join(overlap)}")
            changes.append(concurrent_change(item, "target-created", message, assessment, overlappingRequirements=overlap))
            review_required |= bool(overlap)
        elif existed and path.exists() and digest(path) != item.get("baseFileHash", ""):
            current_hash = digest(path)
            if snapshot is None or not snapshot.exists():
                overlap, assessment = [], "review-required"
                message = f"正式 Spec 已修改，但旧 Change 没有基线快照，需人工判断合并影响：{item['targetSpecPath']}"
            else:
                overlap = sorted(changed_requirement_names(snapshot, path) & set(requirement_blocks(delta)))
                assessment = "merge-safe" if not overlap else "review-required"
                message = (f"正式 Spec 的并发修改未触及 Delta Requirement，可进行保留式合并：{item['targetSpecPath']}" if not overlap
                           else f"正式 Spec 与 Delta 同时修改了 Requirement：{', '.join(overlap)}")
            changes.append(concurrent_change(item, "target-modified", message, assessment,
                                             baseFileHash=item.get("baseFileHash", ""), currentFileHash=current_hash,
                                             overlappingRequirements=overlap))
            review_required |= assessment == "review-required"
    status = "review-required" if review_required else ("merge-safe" if changes else "clean")
    summary = ("检测到可能重叠的正式 Spec 变化；需先判断智能合并是否会覆盖或混杂语义。" if review_required
               else ("检测到正式 Spec 变化，但 Requirement 不重叠，可以保留式合并。" if changes else "正式 Spec 与基线一致，可以 sync。"))
    review["syncValidation"] = {"status": status, "summary": summary, "changes": changes, "conflicts": [], "validatedAt": timestamp()}
    review["specSyncStatus"] = "merge-review-required" if review_required else "pending"
    write(review_path, review)
    print(json.dumps(review["syncValidation"], ensure_ascii=False, indent=2))
    return 3 if review_required else 0


def resolve(args):
    change = Path(args.change_root).resolve()
    review_path = change / "change-review.json"
    review = read(review_path)
    validation = review.get("syncValidation") or {}
    if validation.get("status") != "review-required":
        raise SystemExit("No pending merge review")
    if args.result == "safe":
        validation["status"] = "merge-safe"
        validation["summary"] = args.summary or "已审查并发变化；智能合并可保留双方语义。"
        validation["conflicts"] = []
        review["specSyncStatus"] = "pending"
    else:
        validation["status"] = "conflict"
        validation["summary"] = args.summary or "已确认合并会覆盖功能或混杂语义，sync 已阻止。"
        validation["conflicts"] = validation.get("changes") or []
        review["specSyncStatus"] = "blocked-by-conflict"
    validation["validatedAt"] = timestamp()
    review["syncValidation"] = validation
    write(review_path, review)
    print(json.dumps(validation, ensure_ascii=False, indent=2))
    return 0 if args.result == "safe" else 2


def record(args):
    root, change = Path(args.project_root).resolve(), Path(args.change_root).resolve()
    review_path = change / "change-review.json"
    review = read(review_path)
    synced_at = timestamp()
    for item in review.get("syncTargets") or []:
        path = root / item["targetSpecPath"]
        if not path.exists():
            raise SystemExit(f"Synced target missing: {item['targetSpecPath']}")
        item["syncedFileHash"] = digest(path)
    review["specSyncStatus"], review["specSyncedAt"] = "synced", synced_at
    review["syncValidation"] = {"status": "synced", "summary": "Delta 已合入正式 Spec。", "conflicts": [], "validatedAt": synced_at}
    write(review_path, review)
    return 0


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    for name, handler in (("capture", capture), ("validate", validate), ("resolve-review", resolve), ("record-synced", record)):
        command = commands.add_parser(name)
        command.add_argument("--project-root", required=True)
        command.add_argument("--change-root", required=True)
        if name == "capture":
            command.add_argument("--force", action="store_true")
        if name == "resolve-review":
            command.add_argument("--result", required=True, choices=("safe", "conflict"))
            command.add_argument("--summary", default="")
        command.set_defaults(handler=handler)
    args = parser.parse_args()
    return args.handler(args)


if __name__ == "__main__":
    raise SystemExit(main())
