---
schemaVersion: 2
category: architecture
title: 决战阶段编排
---

# Showdown Phase Orchestration Specification

## Purpose

定义 Boss 决战阶段在全局循环中的场景与界面激活、回合状态机入口，以及终局结果向全局调度层交接的边界；具体战斗算法与胜负规则不在此处定义。

## Requirements

### Requirement: Showdown phase enters through GameManager
The project SHALL enter BossFight through GameManager and activate the BossFight world and UI roots.

#### Scenario: Entering BossFight
- **WHEN** the global phase changes to BossFight
- **THEN** GameManager activates BossFight roots and starts the configured combat turn state machine

### Requirement: Showdown completion returns to global orchestration
Boss defeat and terminal combat outcomes SHALL be reported to GameManager before changing the global phase.

#### Scenario: Boss is defeated
- **WHEN** the BossDefeated event is received
- **THEN** GameManager completes hunt settlement handoff and transitions back to Settlement
