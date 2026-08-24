---
schemaVersion: 2
category: feature
title: "狩猎会话事件链恢复"
---

# Hunt Session Event Occurrence Recovery Specification

## Purpose

让狩猎事件在同一 Hunt session 内遇到 Reactor 阻止、表现确认失败或临时异常时，从明确 occurrence 继续，而不重放已经提交的父节点或放行新的探索命令。

## Requirements

### Requirement: Pending occurrence gates new Hunt gameplay

Hunt Runner SHALL 在执行事件节点前登记 root occurrence。节点提交前失败 SHALL 保留该 occurrence；地块、采集与回营命令 SHALL 先恢复待办事件，恢复失败时不得继续原命令。

#### Scenario: A Reactor prevents the event node before execution

- **WHEN** 地块已经揭示，但事件节点被 Before Reactor 阻止
- **THEN** root occurrence SHALL 保持待恢复
- **AND** 移除阻止规则后的下一条狩猎命令 SHALL 先完成该事件

### Requirement: Post-commit retry does not replay the parent

事件效果和 occurrence 提交 SHALL 位于结果表现确认之前。确认失败后，父 occurrence SHALL 已消费，直接子 occurrence SHALL 按原顺序保留；重试 SHALL 从第一个待办子节点继续。

#### Scenario: Result confirmation fails after a choice commits

- **WHEN** 父事件已经提交效果与两个同 ContentId 的子引用，随后结果确认失败
- **THEN** 父事件 SHALL NOT 再次选择或施加效果
- **AND** 两个子 occurrence SHALL 以不同 Sequence 分别执行

### Requirement: Recovery preserves event identity and actor context

恢复 SHALL 使用 occurrence 冻结的 ActorId 和稳定 ContentId 祖先链。当前 UI 选中猎人不得改变待恢复节点的默认行动者；冻结猎人已经死亡或不存在时，Runner SHALL 确定性选择当前可行动猎人。祖先 ContentId 回边 SHALL 被阻止。

#### Scenario: The player changes the selected hunter before retry

- **WHEN** 猎人 A 提交父事件后确认失败，玩家随后选择猎人 B
- **THEN** 待恢复子事件 SHALL 仍使用存入 occurrence 的猎人 A

### Requirement: Bounded truncation remains a committed outcome

Hunt occurrence store SHALL 限制待办数量。单次提交超过上限或序号空间时，父节点 SHALL 保持已提交，接受的有界前缀 SHALL 保持顺序，超出部分 SHALL 被阻止并发布可诊断的截断事实；系统 SHALL NOT 重放已生效父节点。

#### Scenario: A parent produces more children than the session limit

- **WHEN** 子事件数量超过当前 Hunt store 的待办上限
- **THEN** 有界前缀 SHALL 进入待恢复队列
- **AND** 提交事实 SHALL 包含被截断数量与原因

### Requirement: Encounter handoff pauses the owning session

事件或 Boss 地块请求遭遇时，Hunt session SHALL 在发布交接请求前锁定地块、采集和回营命令。交接失败或异常 SHALL 为同一 SourceSessionId 释放锁；成功交接遵循现有阶段生命周期。

#### Scenario: Encounter startup is rejected

- **WHEN** Campaign Runner 拒绝当前 Hunt session 的遭遇请求
- **THEN** Hunt session SHALL 释放交接锁并允许玩家继续

### Requirement: Recovery scope is session-local

本能力 SHALL NOT 把 Hunt occurrence 写入 Settlement 存档，也 SHALL NOT 声称跨进程或跨遭遇恢复。完整耐久恢复 SHALL 等待包含地图、小队、携带物、随机上下文与 session identity 的 active-Hunt snapshot。

#### Scenario: Hunt transitions to the current BossFight lifecycle

- **WHEN** GameManager 销毁来源 Hunt session
- **THEN** 该 session 的 occurrence store 随所有者释放
- **AND** 当前版本不得宣称可以在战斗后或重启后续接该事件链
