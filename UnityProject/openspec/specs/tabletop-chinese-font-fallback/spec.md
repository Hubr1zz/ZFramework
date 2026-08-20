---
schemaVersion: 2
category: system
title: "桌面中文字体回退"
---

# Tabletop Chinese Font Fallback Specification

## Purpose

确保营地、狩猎和事件中的世界空间卡牌文字在编辑器与可移植构建中始终保持可读；项目随包提供中文字体，同时保留美术通过 Inspector 替换字体与预热字符集的配置入口，不依赖开发机器安装的字体。

## Requirements

### Requirement: Playable builds include a Chinese-capable font
The game SHALL ship a Git-managed Chinese font resource that can produce the glyphs used by core tabletop flow text.

#### Scenario: The game runs on a machine without a configured project font
- **WHEN** `GameManager` initializes localization with no serialized Chinese font asset
- **THEN** localization creates a dynamic TMP font from the bundled resource without using an absolute machine path

### Requirement: Chinese glyphs are available through the global TMP fallback
Localization SHALL register its configured or bundled Chinese font before phase Views create player-facing text.

#### Scenario: A 3D card uses the default TMP font
- **WHEN** its text contains a glyph absent from the default Latin font
- **THEN** TMP resolves the glyph through the registered Chinese fallback instead of displaying the missing-glyph square

### Requirement: Inspector configuration remains authoritative
An explicitly configured dynamic `TMP_FontAsset` SHALL take precedence over the bundled fallback.

#### Scenario: A project-specific Chinese font is assigned
- **WHEN** localization initializes
- **THEN** that font is prewarmed when a character set is supplied and is registered as the global fallback
