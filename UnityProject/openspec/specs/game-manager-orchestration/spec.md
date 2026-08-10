---
schemaVersion: 2
category: architecture
title: "GameManager管理器"
---

# GameManager管理器 Specification

## Purpose

定义项目唯一组合根对全局生命周期、共享服务和三个阶段切换的调度边界；阶段内部的玩法规则与具体表现仍由各自子系统负责，避免全局管理器持续膨胀。

## Requirements

### Requirement: GameManager owns global phase orchestration
The project SHALL use one GameManager composition root to coordinate Settlement, Hunt, and BossFight lifecycle transitions.

#### Scenario: Transitioning between game phases
- **WHEN** a phase transition is requested
- **THEN** GameManager delegates state transition to PhaseManager and activates only the destination phase roots

### Requirement: GameManager delegates domain behavior
GameManager SHALL coordinate phase managers and shared services without implementing their internal gameplay rules.

#### Scenario: Entering a phase
- **WHEN** a destination phase becomes active
- **THEN** GameManager invokes that phase's entry boundary and leaves phase-specific behavior to the corresponding subsystem
