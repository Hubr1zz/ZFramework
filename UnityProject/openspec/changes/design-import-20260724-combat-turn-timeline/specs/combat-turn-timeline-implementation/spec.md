---
schemaVersion: 2
category: feature
title: 战斗回合与时点代码实现
---

# 战斗回合与时点代码实现

## ADDED Requirements

### Requirement: 实现“战斗回合与时点规则设计”
实现 SHALL 以高内聚模块提供全部玩家规则，并只通过显式依赖端口与其他战斗模块协作。

#### Scenario: 独立验证模块
- **WHEN** 测试提供本模块输入与依赖端口替身
- **THEN** 本模块可独立产生可验证结果

### Requirement: 回合与时点规则保持引擎无关
实现 SHALL 由纯 C# owner 持有回合阶段、个人时点、结转和行动资格；Unity Adapter 只发布事件并等待表现完成。

#### Scenario: 确定性推进回合
- **WHEN** 测试依次提交行动、结束与援助命令
- **THEN** 不加载 Unity 场景即可复现阶段、时点和行动资格

#### Scenario: 等待 Boss 行动完成
- **WHEN** Boss 行动包含异步攻击或表现
- **THEN** 回合状态机等待行动完成信号后才进入下一玩家回合
