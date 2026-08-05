#!/usr/bin/env python3
"""Migrate legacy central Draft Specs to paired, Change-only design imports."""

from __future__ import annotations

import hashlib
import json
import shutil
import argparse
from pathlib import Path


VAGUE_FEATURE_SUFFIXES = ("脚本主要职责", "脚本的主要职责", "主要职责")


def read_json(path: Path, default):
    return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else default


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def normalized_hash(path: Path) -> str:
    text = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").strip()
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def merge_by_id(left: list[dict], right: list[dict]) -> list[dict]:
    values = {}
    anonymous = []
    for item in [*left, *right]:
        if item.get("id"):
            values[item["id"]] = item
        elif item not in anonymous:
            anonymous.append(item)
    return [*values.values(), *anonymous]


def coarsen_evidence(project_root: Path, evidence: list[dict]) -> list[dict]:
    by_script = {}
    for item in evidence or []:
        key = (item.get("guid", ""), item.get("displayPath", ""))
        if key == ("", ""):
            continue
        current = by_script.get(key)
        if current is None or int(item.get("line", 1) or 1) < int(current.get("line", 1) or 1):
            by_script[key] = dict(item)
    result = []
    for item in by_script.values():
        relative = item.get("displayPath", "")
        script = project_root / relative
        if not script.exists():
            raise FileNotFoundError(f"代码证据脚本不存在：{relative}")
        stem = script.stem
        existing_feature = str(item.get("feature", "")).strip()
        feature = existing_feature
        if not feature or feature == stem or feature in {f"{stem}脚本", f"{stem} 脚本"} or feature.endswith(VAGUE_FEATURE_SUFFIXES):
            raise ValueError(f"代码证据缺少具体大功能描述，请为 {relative} 补充 feature")
        result.append(
            {
                "guid": item.get("guid", ""),
                "displayPath": relative.replace("\\", "/"),
                "fileHash": file_hash(script),
                "line": max(1, int(item.get("line", 1) or 1)),
                "feature": feature,
            }
        )
    return sorted(result, key=lambda item: (item["displayPath"], item["line"]))


def migrate(project_root: Path) -> dict:
    drafts = project_root / "openspec" / "drafts"
    changes = drafts / "changes"
    index_path = drafts / "index.json"
    index = read_json(index_path, {"schemaVersion": 2, "groups": []})
    groups = {item["capability"]: item for item in index.get("groups", [])}

    # Preflight every destructive target before moving or deleting anything.
    legacy_specs = drafts / "specs"
    if legacy_specs.exists():
        for legacy in legacy_specs.rglob("spec.md"):
            capability = legacy.parent.parent.name
            group = groups.get(capability)
            if not group or not group.get("versions"):
                raise RuntimeError(f"中央 Draft Spec 没有对应 Change：{capability}")
            target = project_root / group["versions"][0].get("draftChangePath", "") / "specs" / capability / "spec.md"
            if not target.exists() or normalized_hash(legacy) != normalized_hash(target):
                raise RuntimeError(f"中央 Draft Spec 与 Change 不一致，停止迁移：{capability}")

    # Derive rule/feature pairs from the run dependency graph.
    pairs = {}
    imports_root = project_root / "openspec" / "design-imports"
    for run_dir in imports_root.iterdir() if imports_root.exists() else []:
        graph = read_json(run_dir / "dependencies.json", {"nodes": [], "edges": []})
        categories = {node.get("id"): node.get("category") for node in graph.get("nodes", [])}
        for edge in graph.get("edges", []):
            source, target = edge.get("from"), edge.get("to")
            if categories.get(source) == "game-rule" and categories.get(target) == "feature" and target == f"{source}-implementation":
                pairs[source] = target

    merged = []
    redirects = {}
    for rule, feature in sorted(pairs.items()):
        rule_group, feature_group = groups.get(rule), groups.get(feature)
        if not rule_group or not feature_group:
            continue
        rule_version = rule_group.get("versions", [])[0]
        feature_version = feature_group.get("versions", [])[0]
        rule_change = project_root / rule_version["draftChangePath"]
        feature_change = project_root / feature_version["draftChangePath"]
        if not rule_change.exists() or not feature_change.exists() or rule_change == feature_change:
            continue

        feature_spec_root = feature_change / "specs" / feature
        target_spec_root = rule_change / "specs" / feature
        if target_spec_root.exists():
            if normalized_hash(target_spec_root / "spec.md") != normalized_hash(feature_spec_root / "spec.md"):
                raise RuntimeError(f"配对 Change 内容冲突：{feature}")
        else:
            shutil.copytree(feature_spec_root, target_spec_root)

        rule_review_path = rule_change / "change-review.json"
        feature_review_path = feature_change / "change-review.json"
        rule_review = read_json(rule_review_path, {})
        feature_review = read_json(feature_review_path, {})
        rule_review.update(
            {
                "schemaVersion": 5,
                "category": "paired",
                "codeReadiness": feature_review.get("codeReadiness", rule_review.get("codeReadiness")),
                "capabilities": [rule, feature],
                "reviewIssues": merge_by_id(rule_review.get("reviewIssues", []), feature_review.get("reviewIssues", [])),
                "gapIds": sorted(set(rule_review.get("gapIds", []) + feature_review.get("gapIds", []))),
                "dependencyIds": sorted(set(rule_review.get("dependencyIds", []) + feature_review.get("dependencyIds", []))),
            }
        )
        verification = feature_review.get("verification", rule_review.get("verification", {}))
        verification["codeEvidence"] = coarsen_evidence(project_root, verification.get("codeEvidence", []))
        rule_review["verification"] = verification
        write_json(rule_review_path, rule_review)

        for capability in (rule, feature):
            review_path = rule_change / "specs" / capability / "spec-review.json"
            review = read_json(review_path, {})
            review["schemaVersion"] = 5
            verification = review.setdefault("verification", {})
            verification["codeEvidence"] = coarsen_evidence(project_root, verification.get("codeEvidence", []))
            write_json(review_path, review)

        for name in ("gaps.json",):
            write_json(rule_change / name, merge_by_id(read_json(rule_change / name, []), read_json(feature_change / name, [])))
        feature_tasks = (feature_change / "tasks.md").read_text(encoding="utf-8-sig")
        (rule_change / "tasks.md").write_text(feature_tasks, encoding="utf-8")
        with (rule_change / "proposal.md").open("a", encoding="utf-8") as stream:
            stream.write(f"\n\n## Paired capability\n\n- `{feature}` 与规则一起审批、实施和同步。\n")

        target_relative = str(rule_change.relative_to(project_root)).replace("\\", "/")
        feature_version.update(
            {
                "draftChangePath": target_relative,
                "specPath": f"{target_relative}/specs/{feature}/spec.md",
                "reviewPath": f"{target_relative}/specs/{feature}/spec-review.json",
                "changeId": rule_change.name,
            }
        )
        rule_version["changeId"] = rule_change.name
        redirects[feature_change.name] = rule_change.name
        shutil.rmtree(feature_change)
        merged.append((rule, feature, rule_change.name))

    # Upgrade any unpaired change and make the Change path the only content path.
    for capability, group in groups.items():
        for version in group.get("versions", []):
            change_path = project_root / version.get("draftChangePath", "")
            if not change_path.exists():
                continue
            version["changeId"] = change_path.name
            version["specPath"] = str((change_path / "specs" / capability / "spec.md").relative_to(project_root)).replace("\\", "/")
            version["reviewPath"] = str((change_path / "specs" / capability / "spec-review.json").relative_to(project_root)).replace("\\", "/")
            review_path = change_path / "specs" / capability / "spec-review.json"
            review = read_json(review_path, {})
            review["schemaVersion"] = 5
            verification = review.setdefault("verification", {})
            verification["codeEvidence"] = coarsen_evidence(project_root, verification.get("codeEvidence", []))
            write_json(review_path, review)
            change_review_path = change_path / "change-review.json"
            change_review = read_json(change_review_path, {})
            change_review["schemaVersion"] = 5
            change_verification = change_review.setdefault("verification", {})
            change_verification["codeEvidence"] = coarsen_evidence(project_root, change_verification.get("codeEvidence", []))
            write_json(change_review_path, change_review)

    index["schemaVersion"] = 2
    write_json(index_path, index)

    capability_paths = {
        capability: group["versions"][0]["specPath"]
        for capability, group in groups.items()
        if group.get("versions")
    }
    for change_dir in changes.iterdir() if changes.exists() else []:
        graph_path = change_dir / "dependencies.json"
        graph = read_json(graph_path, {"nodes": [], "edges": []})
        for node in graph.get("nodes", []):
            if node.get("id") in capability_paths:
                node["specPath"] = capability_paths[node["id"]]
        write_json(graph_path, graph)

    # Rewrite run references and dependency paths.
    for run_dir in imports_root.iterdir() if imports_root.exists() else []:
        refs = []
        run_id = run_dir.name
        for capability, group in groups.items():
            for version in group.get("versions", []):
                if run_id in version.get("runIds", []):
                    refs.append({"capability": capability, "changeId": version["changeId"], "status": group.get("status", "ready")})
        write_json(run_dir / "draft-refs.json", {"schemaVersion": 2, "items": refs})
        graph_path = run_dir / "dependencies.json"
        graph = read_json(graph_path, {"nodes": [], "edges": []})
        for node in graph.get("nodes", []):
            group = groups.get(node.get("id"))
            if group and group.get("versions"):
                node["specPath"] = group["versions"][0]["specPath"]
        write_json(graph_path, graph)

    if legacy_specs.exists():
        for legacy in legacy_specs.rglob("spec.md"):
            capability = legacy.parent.parent.name
            group = groups.get(capability)
            if not group or not group.get("versions"):
                raise RuntimeError(f"中央 Draft Spec 没有对应 Change：{capability}")
            target = project_root / group["versions"][0]["specPath"]
            if not target.exists() or normalized_hash(legacy) != normalized_hash(target):
                raise RuntimeError(f"中央 Draft Spec 与 Change 不一致，停止删除：{capability}")
        shutil.rmtree(legacy_specs)

    return {"mergedPairs": merged, "redirects": redirects, "draftSpecsRemoved": not legacy_specs.exists()}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Migrate legacy central Draft Specs to Change-only storage.")
    parser.add_argument("project_root", nargs="?", default=".", help="Project root containing openspec/.")
    args = parser.parse_args()
    print(json.dumps(migrate(Path(args.project_root).resolve()), ensure_ascii=False, indent=2))
