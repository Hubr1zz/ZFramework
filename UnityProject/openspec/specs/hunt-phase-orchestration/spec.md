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
The project SHALL enter Hunt through GameManager and activate the Hunt world and UI roots. HuntPhaseManager SHALL own Hunt runtime generations and one Hunt composition coordinator; CampaignFlowCoordinator SHALL retain route/departure, retreat persistence and encounter handoff transactions and SHALL provide the settlement departure and Hunt retreat operation ports. GameManager SHALL retain only the compatibility facade and scene binding.

#### Scenario: Entering Hunt
- **WHEN** the global phase changes to Hunt
- **THEN** GameManager supplies the active hunter group through the Hunt phase boundary
- **AND** HuntPhaseManager activates the current ActionSession and initializes or rebinds the Hunt presentation adapters

### Requirement: Required tabletop presentation starts atomically
The Hunt composition coordinator SHALL treat the Hunt map visualizer, 3D squad status board, physical retreat entry and current ActionSession as one required playable startup boundary. Production Hunt startup SHALL fail and deactivate the candidate ActionSession when the Hunt world root is unavailable or any required tabletop adapter cannot initialize. It SHALL NOT silently fall back to screen-space Hunt UI.

#### Scenario: Required 3D presentation is ready
- **WHEN** a Hunt runtime generation becomes playable
- **THEN** the Hunt world contains a map visualizer, a tabletop squad status presentation and a physical retreat entry bound to that generation
- **AND** the production coordinator does not create the legacy screen-space Hunt panels

#### Scenario: Tabletop startup fails
- **WHEN** the Hunt world root is unavailable or a required tabletop adapter fails to initialize
- **THEN** the candidate Hunt ActionSession is deactivated
- **AND** Hunt entry reports failure so the owning campaign transaction can roll back the candidate generation and phase transition

### Requirement: Hunt outcomes return to global orchestration
HuntManager SHALL report Boss encounters, hunt completion and committed checkpoints through callbacks owned by the current Hunt composition coordinator. The coordinator SHALL forward only callbacks whose runtime generation, manager identity and ActionSession are still current to GameManager's cross-phase port.

#### Scenario: Hunt reaches a global transition
- **WHEN** HuntManager reports a Boss encounter or completed hunt
- **THEN** GameManager performs the transition to BossFight or Settlement respectively

#### Scenario: Hunt presentation is recreated after restore

- **WHEN** ActiveHunt restore publishes a new current Hunt generation
- **THEN** the Hunt composition coordinator SHALL bind its visualizer, UI and retreat panel to that generation
- **AND** previous presentation callbacks SHALL NOT remain authoritative
