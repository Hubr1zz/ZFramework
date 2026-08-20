---
schemaVersion: 2
category: system
title: "C# 派生索引"
---

# C# 派生索引 Specification

## Purpose

为 Agent 与开发者提供可再生成、可验证的全量 C# 结构索引，减少重复检索成本；索引只负责定位候选，不替代源码、Unity 资源引用或正式设计事实。

## Requirements

### Requirement: Full project-local source coverage
The system SHALL index every `Assets/**/*.cs` file by default and SHALL publish a deterministic derived index that can be synchronized by Git without becoming an authoritative project fact.

#### Scenario: Building the default index
- **WHEN** a build is requested without explicit exclusions
- **THEN** every C# file under `Assets` is represented exactly once in the index

### Requirement: Portable deterministic publication
The canonical index SHALL contain only project-relative paths and content-derived source fingerprints, and SHALL exclude machine-local roots, file timestamps, and generation timestamps.

#### Scenario: Two worktrees index the same source revision
- **WHEN** the canonical index is rebuilt from identical source bytes in different locations
- **THEN** both worktrees publish byte-identical index files while volatile progress remains local and Git-ignored

### Requirement: Observable manual build
The Unity Agent Workbench SHALL provide a manual asynchronous build entry that reports phase progress and remains responsive while `pwsh` executes the public query script.

#### Scenario: Developer starts a build
- **WHEN** the developer selects “构建全量索引”
- **THEN** the Workbench displays current progress, elapsed time, and the final indexing statistics

### Requirement: Independent coverage verification
The Workbench SHALL compare the index result with the current disk inventory and SHALL only report success when coverage is 100%, with no missing or unexpected paths.

#### Scenario: Source inventory differs from the index
- **WHEN** the disk C# file count or indexed paths do not match the completed result
- **THEN** the Workbench reports a failed or stale state and asks for a rebuild

### Requirement: Incremental extraction with atomic publication
The indexer SHALL reuse unchanged per-file extraction facts and SHALL atomically replace the published cache after rebinding the complete project graph.

#### Scenario: One source file changes
- **WHEN** the existing index is compatible and one C# file changes
- **THEN** only that file is re-extracted, all unchanged file facts are reused, and readers never observe a partially written index

#### Scenario: A Windows file monitor briefly holds the destination
- **WHEN** atomic replacement encounters transient file contention
- **THEN** the indexer retries for a bounded period and either publishes the complete file or reports failure without leaving a partial canonical index
