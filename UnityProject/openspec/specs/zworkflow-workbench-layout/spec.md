---
schemaVersion: 2
category: system
title: "zWorkFlow Workbench 布局安全"
---

# zWorkFlow Workbench Layout Safety Specification

## Purpose

确保 Workbench 页面在最小窗口及嵌套分栏中保持可读、可操作，不因新增内容产生非预期横向溢出。

## Requirements

### Requirement: Container-local width budget
Every nested Workbench panel SHALL size rendered content from its own available width after fixed columns, gaps, style chrome, and scrollbars are deducted.

#### Scenario: OpenSpec Changes renders at minimum size
- **WHEN** the Workbench displays OpenSpec / Changes at `900x600`
- **THEN** detail Markdown, tables, fields, and editors remain within the right-hand viewport without ordinary horizontal scrolling
- **AND** the right-hand panel is capped to the local width remaining after the list, gap, panel chrome, and scrollbar budget

#### Scenario: Shared toolbar renders at minimum size
- **WHEN** the Workbench width is below the single-row toolbar budget
- **THEN** shared toolbar actions reflow into bounded rows without widening any page viewport

### Requirement: Automated layout regression guard
The project SHALL route zWorkFlow UI maintenance through a project-local skill that provides a data-based, all-page layout audit and Unity MCP compilation workflow.

#### Scenario: A Workbench page is changed
- **WHEN** an agent optimizes, extends, or refactors Workbench UI
- **THEN** it reads the project layout skill and runs its audit across every Workbench page and portable template before completion

### Requirement: Project-only maintenance policy
The zWorkFlow UI maintenance skill SHALL remain outside the portable zWorkFlow distribution and its package manifest.

#### Scenario: The migration package is assembled
- **WHEN** zWorkFlow portable assets are collected
- **THEN** `.agents/skills/zworkflow-ui-maintenance` is not included in the package
