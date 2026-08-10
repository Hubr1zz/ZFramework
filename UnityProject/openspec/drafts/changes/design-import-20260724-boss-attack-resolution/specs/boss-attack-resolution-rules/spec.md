---
schemaVersion: 2
category: game-rule
title: "Boss 攻击结算规则"
---

# Boss 攻击结算规则

## ADDED Requirements

### Requirement: Boss 攻击为每次尝试重建命中牌堆
Boss 攻击 SHALL 使用伤害、精准、次数和时点；对每个目标的每次攻击，以 Boss 精准张命中和猎人敏捷张闪避构建临时牌堆，并在每次抽取前重置。

#### Scenario: 多次攻击同一目标
- **WHEN** 攻击次数大于 1
- **THEN** 每次尝试均从完整的精准/敏捷牌堆独立抽取

#### Scenario: 攻击多个目标
- **WHEN** 行动选择多个猎人
- **THEN** 系统为每个目标独立创建并重置其命中牌堆

### Requirement: 命中结果进入受伤流程
命中结果 SHALL 保留至受击部位、护甲和伤害结算完成。

#### Scenario: 抽到命中
- **WHEN** 临时牌堆抽到命中
- **THEN** 系统记录命中、揭示受击部位并向伤势模块提交伤害

#### Scenario: 抽到闪避
- **WHEN** 临时牌堆抽到闪避
- **THEN** 本次攻击不生成伤害请求
