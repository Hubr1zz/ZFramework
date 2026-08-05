#!/usr/bin/env python3
"""Prepare and validate design-document derived OpenSpec staging runs."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

GAP_TYPES = {"missing-dependency"}
GAP_STATUSES = {"open", "accepted", "resolved"}
EDGE_TYPES = {"requires", "integrates-with", "extends", "presents"}
SPEC_CATEGORIES = {"architecture", "feature", "game-rule"}
ALLOWED_DEPENDENCIES = {
    "architecture": {"architecture"},
    "feature": {"architecture", "feature"},
    "game-rule": {"feature"},
}
READINESS = {
    "ready",
    "ready-with-deferred-gaps",
    "blocked-by-design",
    "blocked-by-integration",
    "implemented",
}
REVIEW_ISSUE_TYPES = {"design-conflict", "dependency-missing", "implementation-delta"}
REVIEW_SEVERITIES = {"blocking", "warning", "info"}
REVIEW_STATUSES = {"open", "accepted", "resolved"}
CODE_READINESS = {"unimplemented", "partial", "implemented", "not-applicable"}
TYPE_FILTER_ALIASES = {
    "rules": "规则",
    "rule": "规则",
    "规则": "规则",
    "content": "内容",
    "内容": "内容",
    "art": "美术",
    "visual": "美术",
    "presentation": "美术",
    "美术": "美术",
}
TYPE_FILTER_ORDER = ("规则", "内容", "美术")
UNCERTAINTY = re.compile(r"待定|暂未|尚未|未设计|未实现|可能|建议|或许|TODO", re.IGNORECASE)
WIKI_LINK = re.compile(r"\[\[([^]|#]+)(?:#[^]|]+)?(?:\|[^]]+)?]]")
HEADING = re.compile(r"^#{1,6}\s+(.+?)\s*$")
REQUIREMENT = re.compile(r"^### Requirement:\s+(.+?)\s*$")
VAGUE_CODE_EVIDENCE_FEATURE = re.compile(r"(?:脚本(?:的)?主要职责|主要职责)$", re.IGNORECASE)


def now_iso() -> str:
    return datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")


def read_json(path: Path, default):
    if not path.exists():
        return default
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def is_concrete_code_evidence_feature(feature: object, display_path: object) -> bool:
    value = str(feature or "").strip()
    if not value or VAGUE_CODE_EVIDENCE_FEATURE.search(value):
        return False
    stem = Path(str(display_path or "")).stem
    return value.casefold() not in {
        stem.casefold(),
        f"{stem}脚本".casefold(),
        f"{stem} 脚本".casefold(),
    }


def slug(value: str) -> str:
    ascii_part = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    if ascii_part:
        return ascii_part[:48]
    return hashlib.sha1(value.encode("utf-8")).hexdigest()[:10]


def all_markdown(source: Path) -> list[Path]:
    if source.is_file():
        return [source]
    return sorted(p for p in source.rglob("*.md") if p.is_file())


def source_root(source: dict) -> Path:
    path = source["path"]
    return path.parent if path.is_file() else path


def configured_sources(args, project_root: Path) -> list[dict]:
    raw_sources = list(args.source or [])
    definitions = []
    if raw_sources:
        for raw in raw_sources:
            source_id = ""
            path_value = raw
            if "=" in raw:
                candidate_id, candidate_path = raw.split("=", 1)
                if re.fullmatch(r"[A-Za-z0-9._-]+", candidate_id):
                    source_id, path_value = candidate_id, candidate_path
            path = Path(path_value).resolve()
            source_id = source_id or f"source-{hashlib.sha1(str(path).encode('utf-8')).hexdigest()[:10]}"
            definitions.append({"id": source_id, "path": path})
    else:
        config_path = project_root / "openspec" / "design-source.json"
        configuration = read_json(config_path, {})
        if configuration.get("sources"):
            for item in configuration["sources"]:
                path_value = item.get("path", "")
                if not path_value:
                    continue
                path = Path(path_value)
                path = path.resolve() if path.is_absolute() else (project_root / path).resolve()
                definitions.append({"id": item.get("id", ""), "path": path})
        elif configuration.get("source"):
            path = Path(configuration["source"])
            path = path.resolve() if path.is_absolute() else (project_root / path).resolve()
            definitions.append({"id": "primary", "path": path})

    if not definitions:
        raise ValueError("未配置设计文档路径；请在工作台添加至少一个设计文档路径")

    ids = set()
    paths = set()
    for source in definitions:
        source_id = source["id"]
        if not re.fullmatch(r"[A-Za-z0-9._-]+", source_id or ""):
            raise ValueError(f"设计文档来源 ID 无效：{source_id or '<empty>'}")
        canonical = str(source["path"]).casefold()
        if source_id.casefold() in ids:
            raise ValueError(f"设计文档来源 ID 重复：{source_id}")
        if canonical in paths:
            raise ValueError(f"设计文档路径重复：{source['path']}")
        if not source["path"].exists():
            raise FileNotFoundError(f"设计文档路径不存在：{source['path']}")
        ids.add(source_id.casefold())
        paths.add(canonical)
    return definitions


def select_sources(sources: list[dict], scope: str | None) -> tuple[list[tuple[dict, Path]], list[tuple[dict, Path]], list[dict]]:
    documents = []
    for source in sources:
        documents.extend((source, path) for path in all_markdown(source["path"]))

    if scope:
        needle = scope.casefold()
        selected = []
        for source, path in documents:
            relative = str(path.relative_to(source_root(source))).casefold()
            title_probe = "\n".join(path.read_text(encoding="utf-8-sig").splitlines()[:30]).casefold()
            if needle in relative or needle in title_probe:
                selected.append((source, path))
    else:
        selected = documents

    if not selected:
        raise ValueError(f"没有在已配置路径中找到与 scope 匹配的 Markdown：{scope}")

    by_source_stem: dict[tuple[str, str], list[tuple[dict, Path]]] = {}
    by_stem: dict[str, list[tuple[dict, Path]]] = {}
    for source, path in documents:
        stem = path.stem.casefold()
        by_source_stem.setdefault((source["id"].casefold(), stem), []).append((source, path))
        by_stem.setdefault(stem, []).append((source, path))

    selected_keys = {(source["id"].casefold(), str(path).casefold()) for source, path in selected}
    context: dict[tuple[str, str], tuple[dict, Path]] = {}
    ambiguities = []
    for source, path in selected:
        text = path.read_text(encoding="utf-8-sig")
        for target in WIKI_LINK.findall(text):
            basename = Path(target.replace("\\", "/")).name.casefold()
            local_matches = by_source_stem.get((source["id"].casefold(), basename), [])
            matches = local_matches or by_stem.get(basename, [])
            if len(matches) > 1:
                ambiguities.append(
                    {
                        "source": f"{source['id']}::{str(path.relative_to(source_root(source))).replace(chr(92), '/')}",
                        "target": target,
                        "matches": [
                            f"{item_source['id']}::{str(item_path.relative_to(source_root(item_source))).replace(chr(92), '/')}"
                            for item_source, item_path in matches
                        ],
                    }
                )
                continue
            if len(matches) == 1:
                linked_source, linked_path = matches[0]
                key = (linked_source["id"].casefold(), str(linked_path).casefold())
                if key not in selected_keys:
                    context[key] = (linked_source, linked_path)
    return selected, sorted(context.values(), key=lambda item: (item[0]["id"], str(item[1]))), ambiguities


def source_record(source: dict, path: Path, role: str) -> dict:
    root = source_root(source)
    text = path.read_text(encoding="utf-8-sig")
    headings = [m.group(1) for line in text.splitlines() if (m := HEADING.match(line))]
    return {
        "sourceId": source["id"],
        "path": str(path.resolve()),
        "relativePath": str(path.relative_to(root)).replace("\\", "/"),
        "role": role,
        "sha256": sha256(path),
        "headings": headings,
    }


def uncertainty_candidates(documents: list[tuple[dict, Path]]) -> list[dict]:
    candidates = []
    for source, path in documents:
        for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
            if UNCERTAINTY.search(line):
                candidates.append(
                    {
                        "source": f"{source['id']}::{str(path.relative_to(source_root(source))).replace(chr(92), '/')}:{number}",
                        "text": line.strip(),
                    }
                )
    return candidates


def selected_type_filters(args) -> list[str]:
    requested = list(args.filter or [])
    if args.rules:
        requested.append("rules")
    if args.content:
        requested.append("content")
    if args.art:
        requested.append("art")

    normalized = set()
    for value in requested:
        key = value.strip().casefold()
        if key not in TYPE_FILTER_ALIASES:
            allowed = ", ".join(TYPE_FILTER_ORDER)
            raise ValueError(f"未知设计导入类型：{value}；允许：{allowed}")
        normalized.add(TYPE_FILTER_ALIASES[key])
    return [value for value in TYPE_FILTER_ORDER if value in normalized]


def normalize_set(values: list[str]) -> list[str]:
    return sorted(str(value) for value in values if str(value).strip())


def requirement_titles(spec_root: Path) -> dict[str, list[str]]:
    titles: dict[str, list[str]] = {}
    if not spec_root.exists():
        return titles
    for spec in sorted(spec_root.glob("*/spec.md")):
        capability = spec.parent.name
        matches = [
            match.group(1).strip()
            for line in spec.read_text(encoding="utf-8-sig").splitlines()
            if (match := REQUIREMENT.match(line))
        ]
        if matches:
            titles[capability] = normalize_set(matches)
    return titles


def unpublished_imports(imports_root: Path) -> list[dict]:
    imports = []
    if not imports_root.exists():
        return imports
    for run_dir in sorted(path for path in imports_root.iterdir() if path.is_dir()):
        run = read_json(run_dir / "run.json", {})
        if not run or run.get("status") == "published":
            continue
        sources = read_json(run_dir / "sources.json", [])
        gaps = read_json(run_dir / "gaps.json", [])
        graph = read_json(run_dir / "dependencies.json", {"nodes": [], "edges": []})
        imports.append(
            {
                "runId": run.get("runId") or run_dir.name,
                "runDir": str(run_dir),
                "status": run.get("status", ""),
                "scope": run.get("scope", ""),
                "typeFilters": run.get("typeFilters", []),
                "sources": sources,
                "requirementTitlesByCapability": requirement_titles(run_dir / "specs"),
                "gapIds": normalize_set([gap.get("id", "") for gap in gaps]),
                "dependencyIds": normalize_set([edge.get("id", "") for edge in graph.get("edges", [])]),
                "capabilities": normalize_set([node.get("id", "") for node in graph.get("nodes", [])]),
            }
        )
    return imports


def source_fingerprint(records: list[dict]) -> list[dict]:
    return sorted(
        [
            {
                "sourceId": item.get("sourceId", "primary"),
                "relativePath": item.get("relativePath", ""),
                "role": item.get("role", ""),
                "sha256": item.get("sha256", ""),
            }
            for item in records
            if item.get("role") == "requirement"
        ],
        key=lambda item: (item["sourceId"], item["relativePath"], item["role"], item["sha256"]),
    )


def artifact_inventory(project_root: Path) -> list[dict]:
    """Index every authoritative capability without reading unrelated project code."""
    roots = (
        ("formal-spec", project_root / "openspec" / "specs"),
        ("formal-change", project_root / "openspec" / "changes"),
        ("draft-change", project_root / "openspec" / "drafts" / "changes"),
    )
    artifacts = []
    seen_paths = set()
    for kind, root in roots:
        if not root.exists():
            continue
        pattern = "*/spec.md" if kind == "formal-spec" else "*/specs/*/spec.md"
        for spec_path in sorted(root.glob(pattern)):
            if "archive" in spec_path.relative_to(root).parts:
                continue
            canonical = str(spec_path.resolve()).casefold()
            if canonical in seen_paths:
                continue
            seen_paths.add(canonical)
            capability = spec_path.parent.name
            artifacts.append(
                {
                    "kind": kind,
                    "capability": capability,
                    "contentHash": sha256(spec_path),
                    "specPath": str(spec_path.relative_to(project_root)).replace("\\", "/"),
                    "changeId": "" if kind == "formal-spec" else spec_path.parents[2].name,
                }
            )
    return artifacts


def duplicate_precheck(
    project_root: Path,
    imports_root: Path,
    records: list[dict],
    scope: str,
    type_filters: list[str],
) -> dict:
    current_sources = source_fingerprint(records)
    current_by_path = {
        f"{item['sourceId']}::{item['relativePath']}": item["sha256"]
        for item in current_sources
    }
    current_hashes = {item["sha256"] for item in current_sources if item["sha256"]}
    exact_matches = []
    candidates = []
    for previous in unpublished_imports(imports_root):
        previous_sources = source_fingerprint(previous.get("sources", []))
        previous_by_path = {
            f"{item['sourceId']}::{item['relativePath']}": item["sha256"]
            for item in previous_sources
        }
        source_hash_matches = sorted(current_hashes & {item["sha256"] for item in previous_sources})
        changed_same_paths = sorted(
            path
            for path, digest in current_by_path.items()
            if path in previous_by_path and previous_by_path[path] != digest
        )
        same_scope = previous.get("scope", "") == scope
        same_filters = normalize_set(previous.get("typeFilters", [])) == normalize_set(type_filters)
        if same_scope and same_filters and previous_sources == current_sources:
            exact_matches.append(
                {
                    "runId": previous["runId"],
                    "runDir": previous["runDir"],
                    "reason": "same scope, type filters, requirement source paths, and source hashes",
                }
            )
        if same_scope or source_hash_matches or changed_same_paths:
            candidates.append(
                {
                    "runId": previous["runId"],
                    "runDir": previous["runDir"],
                    "sameScope": same_scope,
                    "sameTypeFilters": same_filters,
                    "sourceHashMatches": source_hash_matches,
                    "changedSamePaths": changed_same_paths,
                    "capabilities": previous.get("capabilities", []),
                    "requirementTitlesByCapability": previous.get("requirementTitlesByCapability", {}),
                    "gapIds": previous.get("gapIds", []),
                    "dependencyIds": previous.get("dependencyIds", []),
                }
            )
    return {
        "schemaVersion": 3,
        "checkedAt": now_iso(),
        "exactMatches": exact_matches,
        "candidates": candidates,
        "artifacts": artifact_inventory(project_root),
        "dedupOrder": ["formal-spec", "formal-change", "draft-change"],
    }


def prepare(args) -> int:
    project_root = Path(args.project_root).resolve()
    sources = configured_sources(args, project_root)
    type_filters = selected_type_filters(args)
    requirements, context, link_ambiguities = select_sources(sources, args.scope)
    run_id = args.run_id or (
        datetime.now().strftime("%Y%m%d-%H%M%S")
        + "-"
        + slug(args.scope or "all-design-sources")
    )
    imports_root = (
        Path(args.output_root).resolve()
        if args.output_root
        else project_root / "openspec" / "design-imports"
    )
    records = [source_record(source, path, "requirement") for source, path in requirements]
    records += [source_record(source, path, "context") for source, path in context]
    precheck = duplicate_precheck(project_root, imports_root, records, args.scope or "", type_filters)
    run_dir = imports_root / run_id
    if run_dir.exists():
        raise FileExistsError(f"导入批次已存在：{run_dir}")
    run_dir.mkdir(parents=True)
    drafts_root = project_root / "openspec" / "drafts"
    (drafts_root / "changes").mkdir(parents=True, exist_ok=True)

    candidates = uncertainty_candidates(requirements)
    omitted_types = [value for value in TYPE_FILTER_ORDER if type_filters and value not in type_filters]
    run = {
        "schemaVersion": 5,
        "runId": run_id,
        "status": "staged",
        "generationState": "awaiting-agent",
        "sourceRoots": [{"id": source["id"], "path": str(source["path"])} for source in sources],
        "scope": args.scope or "",
        "typeFilters": type_filters,
        "projectRoot": str(project_root),
        "createdAt": now_iso(),
        "publishedAt": "",
        "publicationStatus": "",
        "duplicatePrecheck": {
            "exactMatchCount": len(precheck["exactMatches"]),
            "candidateCount": len(precheck["candidates"]),
        },
        "documentCounts": {
            "requirements": len(requirements),
            "context": len(context),
        },
        "uncertaintyCandidates": candidates,
        "ambiguousLinks": link_ambiguities,
        "typeFilterAudit": {
            "selectedTypes": type_filters or TYPE_FILTER_ORDER,
            "omittedTypes": omitted_types,
            "preservedConstraints": [],
            "mixedDescriptions": [],
        },
    }
    write_json(run_dir / "run.json", run)
    write_json(run_dir / "sources.json", records)
    write_json(run_dir / "duplicate-precheck.json", precheck)
    write_json(run_dir / "gaps.json", [])
    write_json(run_dir / "dependencies.json", {"schemaVersion": 2, "nodes": [], "edges": []})
    write_json(run_dir / "draft-refs.json", {"items": []})

    print(json.dumps({"runId": run_id, "runDir": str(run_dir), "precheck": precheck}, ensure_ascii=False))
    return 0


def gap_errors(gaps: list[dict], allow_publish: bool) -> list[str]:
    errors = []
    required = {
        "id",
        "capability",
        "requirement",
        "dependencyId",
        "missingNodeId",
        "expectedCategory",
        "type",
        "severity",
        "status",
        "blocksImplementation",
        "blockedScenarios",
        "summary",
        "impact",
        "recommendation",
        "sourceReferences",
    }
    seen = set()
    for index, gap in enumerate(gaps):
        missing = sorted(required - set(gap))
        if missing:
            errors.append(f"gap[{index}] 缺少字段：{', '.join(missing)}")
            continue
        gap_id = gap["id"]
        if gap_id in seen:
            errors.append(f"重复 gap id：{gap_id}")
        seen.add(gap_id)
        if gap["type"] not in GAP_TYPES:
            errors.append(f"{gap_id} type 非法：{gap['type']}")
        if gap.get("expectedCategory") not in SPEC_CATEGORIES:
            errors.append(f"{gap_id} expectedCategory 非法：{gap.get('expectedCategory')}")
        if gap["status"] not in GAP_STATUSES:
            errors.append(f"{gap_id} status 非法：{gap['status']}")
        if allow_publish and gap["status"] == "open":
            errors.append(f"{gap_id} 尚未处理，不能发布")
        if gap["status"] == "accepted":
            for field in (
                "userRationale",
                "deliveryBoundary",
                "implementationImpact",
                "acceptedBy",
                "acceptedAt",
            ):
                if not str(gap.get(field, "")).strip():
                    errors.append(f"{gap_id} accepted 缺少 {field}")
    return errors


def dependency_errors(graph: dict) -> list[str]:
    errors = []
    nodes = graph.get("nodes", [])
    edges = graph.get("edges", [])
    node_ids = {node.get("id") for node in nodes}
    node_by_id = {node.get("id"): node for node in nodes if node.get("id")}
    if None in node_ids:
        errors.append("dependency node 缺少 id")
    for node in nodes:
        category = node.get("category")
        if category not in SPEC_CATEGORIES:
            errors.append(f"{node.get('id')} category 非法：{category}")
        if node.get("readiness", "ready") not in READINESS:
            errors.append(f"{node.get('id')} readiness 非法")
    edge_ids = set()
    for edge in edges:
        edge_id = edge.get("id")
        if not edge_id:
            errors.append("dependency edge 缺少 id")
        elif edge_id in edge_ids:
            errors.append(f"重复 edge id：{edge_id}")
        edge_ids.add(edge_id)
        if edge.get("type") not in EDGE_TYPES:
            errors.append(f"{edge_id} type 非法")
        if edge.get("from") not in node_ids or edge.get("to") not in node_ids:
            errors.append(f"{edge_id} 引用了不存在的 node")
            continue
        source_category = node_by_id[edge["from"]].get("category")
        target_category = node_by_id[edge["to"]].get("category")
        if target_category not in ALLOWED_DEPENDENCIES.get(source_category, set()):
            errors.append(f"{edge_id} 依赖方向非法：{source_category} -> {target_category}")
    return errors


def review_issue_errors(issues: list[dict], allow_publish: bool) -> list[str]:
    errors = []
    seen = set()
    required = {"id", "type", "severity", "status", "blocksApproval", "summary", "sourceId"}
    for index, issue in enumerate(issues):
        missing = sorted(required - set(issue))
        if missing:
            errors.append(f"reviewIssue[{index}] 缺少字段：{', '.join(missing)}")
            continue
        issue_id = issue["id"]
        if issue_id in seen:
            errors.append(f"重复 review issue id：{issue_id}")
        seen.add(issue_id)
        if issue.get("type") not in REVIEW_ISSUE_TYPES:
            errors.append(f"{issue_id} type 非法：{issue.get('type')}")
        if issue.get("severity") not in REVIEW_SEVERITIES:
            errors.append(f"{issue_id} severity 非法：{issue.get('severity')}")
        if issue.get("status") not in REVIEW_STATUSES:
            errors.append(f"{issue_id} status 非法：{issue.get('status')}")
        if issue.get("severity") == "blocking" and issue.get("status") == "accepted":
            errors.append(f"{issue_id} blocking 问题不能接受，必须解决")
        if issue.get("status") == "accepted":
            for field in ("acceptedBy", "acceptedAt", "acceptanceNote"):
                if not str(issue.get(field, "")).strip():
                    errors.append(f"{issue_id} accepted 缺少 {field}")
        if allow_publish and issue.get("blocksApproval") and issue.get("status") not in {"resolved", "accepted"}:
            errors.append(f"{issue_id} 尚未处理，不能批准")
    return errors


def editor_guidance_errors(review: dict, review_path: Path) -> list[str]:
    errors = []
    guidance = review.get("editorGuidance")
    if guidance is None:
        return errors
    category = review.get("category")
    if category not in {"feature", "architecture"}:
        errors.append(f"{review_path} 只有 feature/architecture 可以包含 editorGuidance")
    if not isinstance(guidance, dict):
        return errors + [f"{review_path} editorGuidance 必须是对象"]

    allowed_fields = {
        "summary",
        "inspectorReferences",
        "tunableParameters",
        "sceneSetup",
        "usage",
    }
    unknown_fields = sorted(set(guidance) - allowed_fields)
    if unknown_fields:
        errors.append(f"{review_path} editorGuidance 包含未知字段：{', '.join(unknown_fields)}")
    if "summary" in guidance and not isinstance(guidance.get("summary"), str):
        errors.append(f"{review_path} editorGuidance.summary 必须是字符串")
    action_count = 0
    for field in ("inspectorReferences", "tunableParameters", "sceneSetup", "usage"):
        items = guidance.get(field, [])
        if not isinstance(items, list) or any(
            not isinstance(item, str) or not item.strip() for item in items
        ):
            errors.append(f"{review_path} editorGuidance.{field} 必须是非空字符串数组")
            continue
        action_count += len(items)
    if action_count == 0:
        errors.append(f"{review_path} editorGuidance 至少需要一条可执行指引")
    return errors


def spec_errors(spec_root: Path, project_root: Path | None = None) -> list[str]:
    errors = []
    specs = sorted(spec_root.rglob("spec.md")) if spec_root.exists() else []
    if not specs:
        return ["中央 Draft Store 没有 spec.md"]
    for path in specs:
        text = path.read_text(encoding="utf-8-sig")
        delta_headers = (
            "## ADDED Requirements",
            "## MODIFIED Requirements",
            "## REMOVED Requirements",
            "## RENAMED Requirements",
        )
        if not any(header in text for header in delta_headers):
            errors.append(f"{path} 缺少 OpenSpec delta 标题")
        if re.search(r"^## Requirements\s*$", text, re.MULTILINE):
            errors.append(f"{path} Change Spec 禁止使用裸 `## Requirements`")
        for token in (
            "### Requirement:",
            "#### Scenario:",
            "- **WHEN**",
            "- **THEN**",
        ):
            if token not in text:
                errors.append(f"{path} 缺少 `{token}`")
        review_path = path.with_name("spec-review.json")
        if not review_path.exists():
            errors.append(f"{path} 缺少 spec-review.json")
            continue
        review = read_json(review_path, {})
        for field in (
            "schemaVersion",
            "capability",
            "category",
            "readiness",
            "verification",
            "gapIds",
            "dependencyIds",
            "reviewIssues",
        ):
            if field not in review:
                errors.append(f"{review_path} 缺少 `{field}`")
        if review.get("category") not in SPEC_CATEGORIES:
            errors.append(f"{review_path} category 非法：{review.get('category')}")
        if review.get("schemaVersion", 0) < 5:
            errors.append(f"{review_path} schemaVersion 必须为 5+")
        errors += editor_guidance_errors(review, review_path)
        category = review.get("category")
        paired_field = "pairedFeatureCapability" if category == "game-rule" else "pairedRuleCapability"
        paired_capability = str(review.get(paired_field, "")).strip()
        if paired_capability:
            paired_review_path = spec_root / paired_capability / "spec-review.json"
            if not paired_review_path.exists():
                errors.append(f"{review_path} {paired_field} 指向不存在的 capability：{paired_capability}")
            else:
                paired_review = read_json(paired_review_path, {})
                expected_category = "feature" if category == "game-rule" else "game-rule"
                if paired_review.get("category") != expected_category:
                    errors.append(
                        f"{review_path} {paired_field} 必须指向 {expected_category} capability"
                    )
        if category == "game-rule" and paired_capability:
            verification = review.get("verification", {})
            owned_values = {
                "verification.codeEvidence": verification.get("codeEvidence", []),
                "verification.differences": verification.get("differences", []),
                "gapIds": review.get("gapIds", []),
                "dependencyIds": review.get("dependencyIds", []),
                "reviewIssues": review.get("reviewIssues", []),
            }
            for field, value in owned_values.items():
                if value:
                    errors.append(f"{review_path} 配对 Game Rule 不得承载 {field}；应移至 Feature review")
        for evidence in review.get("verification", {}).get("codeEvidence", []):
            if not str(evidence.get("guid", "")).strip():
                errors.append(f"{review_path} codeEvidence 缺少 guid")
            evidence_status = str(evidence.get("status", "")).strip().casefold()
            if evidence_status and evidence_status not in {"verified", "invalid"}:
                errors.append(f"{review_path} codeEvidence.status 非法：{evidence_status}")
            if evidence_status in {"verified", "invalid"} and not str(
                evidence.get("checkedAt", "")
            ).strip():
                errors.append(f"{review_path} codeEvidence.{evidence_status} 缺少 checkedAt")
            digest = str(evidence.get("fileHash", ""))
            if not re.fullmatch(r"[0-9a-fA-F]{64}", digest):
                errors.append(f"{review_path} codeEvidence.fileHash 必须是脚本 SHA-256")
            elif project_root is not None and evidence.get("displayPath"):
                current = project_root / str(evidence["displayPath"])
                if not current.exists():
                    errors.append(f"{review_path} 代码证据脚本不存在：{evidence['displayPath']}")
                elif evidence_status == "invalid":
                    errors.append(
                        f"{review_path} 代码证据状态为 {evidence_status}，必须重新核验："
                        f"{evidence['displayPath']}"
                    )
            feature = str(evidence.get("feature", "")).strip()
            broad_review_labels = {
                str(review.get("capability", "")).strip().casefold(),
                str(review.get("title", "")).strip().casefold(),
            }
            if (
                not is_concrete_code_evidence_feature(feature, evidence.get("displayPath"))
                or feature.casefold() in broad_review_labels
            ):
                errors.append(
                    f"{review_path} codeEvidence.feature 必须具体描述代码功能，"
                    "不能只写脚本名、capability/title 或‘脚本主要职责’"
                )
        errors += review_issue_errors(review.get("reviewIssues", []), False)
    return errors


def draft_change_errors(draft_root: Path) -> list[str]:
    errors = []
    changes = sorted(path for path in draft_root.iterdir() if path.is_dir()) if draft_root.exists() else []
    for change in changes:
        for name in (
            "proposal.md",
            "design.md",
            "tasks.md",
            "change-review.json",
            "dependencies.json",
            "gaps.json",
            "sources.json",
        ):
            if not (change / name).exists():
                errors.append(f"{change} 缺少 {name}")
        delta_specs = sorted((change / "specs").glob("*/spec.md")) if (change / "specs").exists() else []
        if not delta_specs:
            errors.append(f"{change} 缺少 specs/<capability>/spec.md")
        for spec in delta_specs:
            if not spec.with_name("spec-review.json").exists():
                errors.append(f"{spec} 缺少 spec-review.json")
        review_path = change / "change-review.json"
        if review_path.exists():
            review = read_json(review_path, {})
            if review.get("schemaVersion", 0) < 5:
                errors.append(f"{review_path} schemaVersion 必须为 5+")
            if review.get("approvalStatus") not in {"draft", "implementation-change"}:
                errors.append(f"{review_path} approvalStatus 非法")
            if review.get("codeReadiness") not in CODE_READINESS:
                errors.append(f"{review_path} codeReadiness 非法")
            errors += review_issue_errors(review.get("reviewIssues", []), False)
    return errors


def duplicate_artifact_errors(project_root: Path) -> list[str]:
    """Reject byte-identical capability content and duplicate Draft ownership."""
    errors = []
    artifacts = artifact_inventory(project_root)
    by_capability: dict[str, list[dict]] = {}
    for item in artifacts:
        by_capability.setdefault(item["capability"], []).append(item)
    for capability, items in by_capability.items():
        drafts = [item for item in items if item["kind"] == "draft-change"]
        draft_changes = {item["changeId"] for item in drafts}
        if len(draft_changes) > 1:
            errors.append(f"{capability} 同时存在于多个 Draft Change；必须原位合并")
        by_hash: dict[str, list[dict]] = {}
        for item in items:
            by_hash.setdefault(item["contentHash"], []).append(item)
        for duplicates in by_hash.values():
            locations = {item["specPath"] for item in duplicates}
            if len(locations) > 1:
                errors.append(
                    f"{capability} 存在完全相同的重复内容：" + ", ".join(sorted(locations))
                )
    return errors


def dependency_gap_errors(gaps: list[dict], graph: dict) -> list[str]:
    errors = []
    gaps_by_id = {gap.get("id"): gap for gap in gaps if gap.get("id")}
    edges_by_id = {edge.get("id"): edge for edge in graph.get("edges", []) if edge.get("id")}
    for gap_id, gap in gaps_by_id.items():
        dependency_id = gap.get("dependencyId")
        edge = edges_by_id.get(dependency_id)
        if edge is None:
            errors.append(f"{gap_id} dependencyId 未指向依赖边：{dependency_id}")
            continue
        if edge.get("gapId") != gap_id:
            errors.append(f"{dependency_id} gapId 与 {gap_id} 不一致")
        if edge.get("to") != gap.get("missingNodeId"):
            errors.append(f"{gap_id} missingNodeId 与依赖边终点不一致")
    for edge_id, edge in edges_by_id.items():
        gap_id = edge.get("gapId")
        if gap_id and gap_id not in gaps_by_id:
            errors.append(f"{edge_id} gapId 未指向 Gap：{gap_id}")
    return errors


def paired_draft_errors(run: dict, draft_refs: dict, graph: dict, drafts_root: Path) -> list[str]:
    """Validate the rule -> implementation Draft contract for newly prepared runs."""
    if int(run.get("schemaVersion", 0)) < 3:
        return []

    errors = []
    refs = {
        item.get("capability"): item
        for item in draft_refs.get("items", [])
        if item.get("capability") and item.get("changeId")
    }
    nodes = {node.get("id"): node for node in graph.get("nodes", []) if node.get("id")}
    edges = graph.get("edges", [])

    for capability, item in refs.items():
        change_id = item["changeId"]
        review_path = drafts_root / "changes" / change_id / "specs" / capability / "spec-review.json"
        if not review_path.exists():
            continue
        review = read_json(review_path, {})
        if review.get("category") != "game-rule":
            continue

        feature_targets = [
            edge.get("to")
            for edge in edges
            if edge.get("from") == capability
            and nodes.get(edge.get("to"), {}).get("category") == "feature"
        ]
        if not feature_targets:
            errors.append(f"{capability} 缺少 game-rule -> feature 依赖")
            continue

        for target in feature_targets:
            target_ref = refs.get(target)
            formal_spec = drafts_root.parent / "specs" / target / "spec.md"
            if target_ref is None and not formal_spec.exists():
                errors.append(f"{capability} 的 Feature {target} 既非同批次 Draft 也非正式 Spec")
                continue
            if target_ref is None:
                continue
            target_spec = drafts_root / "changes" / target_ref["changeId"] / "specs" / target / "spec.md"
            if not target_spec.exists():
                errors.append(f"{target} 缺少配套 Feature spec.md")
                continue
            text = target_spec.read_text(encoding="utf-8-sig")
            if "### Requirement: 实现“" not in text or "设计”" not in text:
                errors.append(f"{target_spec} 缺少 `实现“<规则标题>设计”` 总括 Requirement")
    return errors


def validate_run(run_dir: Path, allow_publish: bool) -> list[str]:
    errors = []
    for name in ("run.json", "sources.json", "gaps.json", "dependencies.json", "draft-refs.json"):
        if not (run_dir / name).exists():
            errors.append(f"缺少 {name}")
    if errors:
        return errors
    gaps = read_json(run_dir / "gaps.json", [])
    graph = read_json(run_dir / "dependencies.json", {})
    run = read_json(run_dir / "run.json", {})
    draft_refs = read_json(run_dir / "draft-refs.json", {"items": []})
    errors += gap_errors(gaps, allow_publish)
    errors += dependency_errors(graph)
    errors += dependency_gap_errors(gaps, graph)
    drafts_root = run_dir.parent.parent / "drafts"
    project_root = run_dir.parent.parent.parent
    errors += duplicate_artifact_errors(project_root)
    errors += paired_draft_errors(run, draft_refs, graph, drafts_root)
    legacy_specs_root = drafts_root / "specs"
    if legacy_specs_root.exists() and any(legacy_specs_root.rglob("spec.md")):
        errors.append("openspec/drafts/specs 已废止；导入 Spec 只能存在于 Draft Change")
    referenced_changes = {
        item.get("changeId") for item in draft_refs.get("items", []) if item.get("changeId")
    }
    for item in draft_refs.get("items", []):
        if not item.get("capability") or not item.get("changeId"):
            errors.append("draft-refs.json 每项必须包含 capability 与 changeId")
    for change_id in referenced_changes:
        change_root = drafts_root / "changes" / change_id
        if not change_root.exists():
            errors.append(f"draft-refs.json 引用了不存在的 Draft Change：{change_id}")
            continue
        errors += spec_errors(change_root / "specs", project_root)
    errors += draft_change_errors(drafts_root / "changes")
    if allow_publish:
        for review_path in (drafts_root / "changes").glob("*/change-review.json"):
            review = read_json(review_path, {})
            errors += review_issue_errors(review.get("reviewIssues", []), True)
    return errors


def validate(args) -> int:
    run_dir = Path(args.run_dir).resolve()
    errors = validate_run(run_dir, args.allow_publish)
    result = {"valid": not errors, "errors": errors}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if not errors else 1


def is_hard_gap(gap: dict) -> bool:
    return bool(gap.get("blocksImplementation")) and gap.get("status") != "resolved"


def recompute_graph(run_dir: Path) -> dict:
    gaps = read_json(run_dir / "gaps.json", [])
    graph = read_json(run_dir / "dependencies.json", {"nodes": [], "edges": []})
    by_capability: dict[str, list[dict]] = {}
    for gap in gaps:
        by_capability.setdefault(gap.get("capability", ""), []).append(gap)

    for node in graph.get("nodes", []):
        node_id = node.get("id", "")
        node_gaps = by_capability.get(node_id, [])
        hard_edges = [
            edge
            for edge in graph.get("edges", [])
            if edge.get("from") == node_id
            and edge.get("type") in {"requires", "integrates-with"}
            and edge.get("status", "open") != "resolved"
            and edge.get("blocksImplementation", True)
        ]
        hard_gaps = [gap for gap in node_gaps if is_hard_gap(gap)]
        if hard_gaps or hard_edges:
            node["readiness"] = "blocked-by-integration"
        elif any(gap.get("status") != "resolved" for gap in node_gaps):
            node["readiness"] = "ready-with-deferred-gaps"
        elif node.get("readiness") != "implemented":
            node["readiness"] = "ready"
    write_json(run_dir / "dependencies.json", graph)
    return graph


def recompute(args) -> int:
    graph = recompute_graph(Path(args.run_dir).resolve())
    print(json.dumps(graph, ensure_ascii=False, indent=2))
    return 0


def accept_gap(args) -> int:
    run_dir = Path(args.run_dir).resolve()
    gaps = read_json(run_dir / "gaps.json", [])
    gap = next((item for item in gaps if item.get("id") == args.gap_id), None)
    if gap is None:
        raise ValueError(f"找不到 gap：{args.gap_id}")
    gap.update(
        {
            "status": "accepted",
            "userRationale": args.rationale.strip(),
            "deliveryBoundary": args.delivery_boundary.strip(),
            "implementationImpact": args.implementation_impact.strip(),
            "acceptedBy": args.actor.strip(),
            "acceptedAt": now_iso(),
        }
    )
    write_json(run_dir / "gaps.json", gaps)
    recompute_graph(run_dir)
    print(json.dumps(gap, ensure_ascii=False, indent=2))
    return 0


def merge_by_id(existing: list[dict], incoming: list[dict]) -> list[dict]:
    merged = {item.get("id"): item for item in existing if item.get("id")}
    for item in incoming:
        item_id = item.get("id")
        if not item_id:
            continue
        previous = merged.get(item_id, {})
        combined = dict(previous)
        combined.update(item)
        for field in ("userRationale", "deliveryBoundary", "implementationImpact", "acceptedBy",
                      "acceptedAt", "resolutionNote"):
            if not combined.get(field) and previous.get(field):
                combined[field] = previous[field]
        merged[item_id] = combined
    return sorted(merged.values(), key=lambda item: item.get("id", ""))


def publish_metadata(args) -> int:
    print(
        json.dumps(
            {
                "published": False,
                "error": (
                    "publish-metadata 已停用；请审批并将 openspec/drafts/changes/<change-id> "
                    "整体移动到 openspec/changes，再通过 sync/archive 正式化"
                ),
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return 1


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser()
    commands = root.add_subparsers(dest="command", required=True)

    prepare_cmd = commands.add_parser("prepare")
    prepare_cmd.add_argument("--project-root", required=True)
    prepare_cmd.add_argument(
        "--source",
        action="append",
        default=[],
        metavar="[ID=]PATH",
        help="可重复；省略时读取 openspec/design-source.json，所有路径均为等价正式设计来源",
    )
    prepare_cmd.add_argument("--scope")
    prepare_cmd.add_argument(
        "--filter",
        action="append",
        default=[],
        metavar="TYPE",
        help="可重复：rules/content/art（也接受中文类型名）",
    )
    prepare_cmd.add_argument("--规则", "--rules", dest="rules", action="store_true")
    prepare_cmd.add_argument("--内容", "--content", dest="content", action="store_true")
    prepare_cmd.add_argument("--美术", "--art", dest="art", action="store_true")
    prepare_cmd.add_argument("--run-id")
    prepare_cmd.add_argument("--output-root")
    prepare_cmd.add_argument("--force", action="store_true", help="兼容旧调用；来源预检现在不会直接跳过批次")
    prepare_cmd.set_defaults(func=prepare)

    validate_cmd = commands.add_parser("validate")
    validate_cmd.add_argument("--run-dir", required=True)
    validate_cmd.add_argument("--allow-publish", action="store_true")
    validate_cmd.set_defaults(func=validate)

    recompute_cmd = commands.add_parser("recompute")
    recompute_cmd.add_argument("--run-dir", required=True)
    recompute_cmd.set_defaults(func=recompute)

    accept_cmd = commands.add_parser("accept-gap")
    accept_cmd.add_argument("--run-dir", required=True)
    accept_cmd.add_argument("--gap-id", required=True)
    accept_cmd.add_argument("--actor", required=True)
    accept_cmd.add_argument("--rationale", required=True)
    accept_cmd.add_argument("--delivery-boundary", required=True)
    accept_cmd.add_argument("--implementation-impact", required=True)
    accept_cmd.set_defaults(func=accept_gap)

    publish_cmd = commands.add_parser("publish-metadata")
    publish_cmd.add_argument("--run-dir", required=True)
    publish_cmd.add_argument("--project-root", required=True)
    publish_cmd.set_defaults(func=publish_metadata)
    return root


def main() -> int:
    args = parser().parse_args()
    try:
        return args.func(args)
    except Exception as exc:  # concise CLI error for agent workflows
        print(json.dumps({"error": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
