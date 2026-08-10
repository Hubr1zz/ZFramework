---
schemaVersion: 2
category: architecture
title: 营地阶段编排
---

# Settlement Phase Orchestration Specification

## Purpose

定义营地阶段在全局循环中的进入、场景与界面根节点激活、待结算记录接收及持久化边界；营地内部的经济与角色成长规则不属于本架构 Spec。

## Requirements

### Requirement: Settlement phase enters through GameManager
The project SHALL enter Settlement through GameManager and activate the Settlement world and UI roots.

#### Scenario: Entering Settlement
- **WHEN** the global phase changes to Settlement
- **THEN** GameManager activates Settlement roots, invokes SettlementManager entry, and refreshes the configured Settlement presentation

### Requirement: Settlement entry owns the save boundary
The project SHALL persist settlement state after the Settlement entry lifecycle has completed.

#### Scenario: Completing Settlement entry
- **WHEN** SettlementManager has received any pending hunt record
- **THEN** GameManager saves the resulting settlement state through the persistence adapter
