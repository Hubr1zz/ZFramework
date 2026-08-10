---
schemaVersion: 2
category: architecture
title: 狩猎阶段编排
---

# Hunt Phase Orchestration Specification

## Purpose

定义狩猎阶段的进入、场景与界面根节点激活、队伍参数传递，以及向决战或营地阶段交接的架构边界；具体狩猎玩法由下层 Feature 与规则 Spec 描述。

## Requirements

### Requirement: Hunt phase enters through GameManager
The project SHALL enter Hunt through GameManager and activate the Hunt world and UI roots.

#### Scenario: Entering Hunt
- **WHEN** the global phase changes to Hunt
- **THEN** GameManager supplies the active hunter group to HuntManager and initializes Hunt presentation adapters

### Requirement: Hunt outcomes return to global orchestration
HuntManager SHALL report Boss encounters and hunt completion through callbacks owned by GameManager.

#### Scenario: Hunt reaches a global transition
- **WHEN** HuntManager reports a Boss encounter or completed hunt
- **THEN** GameManager performs the transition to BossFight or Settlement respectively
