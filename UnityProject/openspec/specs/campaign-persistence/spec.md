---
schemaVersion: 2
category: feature
title: 战役持久化与恢复
---

# Campaign Persistence Specification

## Purpose

保证营地长期状态在正常操作、立即退出与继续战役之间保持一致，并在加载后重建 3D 桌面所需的运行时引用。

## Requirements

### Requirement: Committed settlement state is persisted

营地 ActionQueue 提交的资源、发明、工坊、猎人和装备变化 SHALL 通过持久化 Adapter 异步保存；View SHALL NOT 直接写入存档。

#### Scenario: A settlement transaction commits

- **WHEN** Settlement runner 发布已提交事务
- **THEN** GameManager SHALL 请求保存同一份 SettlementInstance 权威状态

### Requirement: Application exit flushes the latest snapshot

应用退出时 SHALL 在生命周期取消令牌失效前同步刷新当前营地快照；退出快照 SHALL 使用与后台保存相同的版本门禁，使较旧任务不能覆盖较新状态。

#### Scenario: The player exits immediately after state changes

- **WHEN** 当前营地状态已经变化但后台保存尚未完成
- **THEN** 退出流程 SHALL 落盘最新快照，较旧后台写入 SHALL 被忽略

### Requirement: Continue restores playable runtime state

继续战役 SHALL 恢复年份、资源、发明、工坊、猎人和稳定物品 ContentId，并通过内容目录重建非序列化装备实例后再释放 3D 营地桌面；旧版显示名键 SHALL 在内容注册后幂等迁移，未知内容 SHALL 保留而不是静默丢弃。

#### Scenario: A saved hunter has equipment

- **WHEN** 玩家从 3D 开场卡继续已有战役
- **THEN** 猎人的运行时装备集合 SHALL 与持久化装备 ContentId 一致，营地桌面 SHALL 显示恢复后的权威状态

#### Scenario: A legacy save uses display names

- **WHEN** 已注册物品的旧存档以显示名保存资源、仓库或猎人装备
- **THEN** 加载流程 SHALL 合并并转换为稳定 ContentId、只提升受支持的身份版本，重复执行迁移 SHALL NOT 重复库存或装备实例

#### Scenario: A save contains unknown or future content

- **WHEN** 存档包含当前目录无法解析的物品标识，或身份版本高于当前运行时
- **THEN** 未知标识 SHALL 保留，未来版本状态 SHALL NOT 被当前运行时降级或重写

### Requirement: Continue rebuilds pending settlement event execution

继续战役 SHALL 把持久化 Timeline 中未完成的事件引用按原顺序解析为当前内容，并投影到新的 Settlement ActionQueue；恢复 SHALL NOT 再次推进年份、抽取随机事件或新增 Timeline 条目。

#### Scenario: A settlement event was pending when the player exited

- **WHEN** 继续战役加载包含未完成与已完成 Timeline 事件的有效存档
- **THEN** 只有未完成事件 SHALL 进入 Settlement Runner，完成前出猎 SHALL 被权威命令边界拒绝
- **AND** 最后一个恢复事件完成后，正常营地操作与出猎 SHALL 恢复

#### Scenario: The save has no pending settlement event

- **WHEN** Timeline 为空或其中事件均已完成
- **THEN** 加载流程 SHALL 建立空恢复投影并正常开放营地，且 SHALL NOT 生成额外事件

#### Scenario: A pending event reference cannot be resolved

- **WHEN** 未完成 Timeline 条目缺少稳定事件 ID，或当前内容目录无法解析该 ID
- **THEN** 加载流程 SHALL 报告可诊断失败并保持出猎门禁，且 SHALL NOT 静默完成、删除或跳过该条目

### Requirement: Committed event chains remain recoverable

事件节点的效果、Timeline 完成状态与直接子 occurrence SHALL 在同一同步状态边界内提交，并在发布保存通知前完成。检查点 SHALL 使用稳定事件 ID、链 ID 与 occurrence 序号，不序列化运行时资产引用；结果确认或后续节点失败 SHALL NOT 丢失已提交父节点产生的子链。

#### Scenario: Result confirmation fails after the parent commits

- **WHEN** 父事件效果已经提交并产生直接子事件，但结果确认表现取消或抛出异常
- **THEN** 父效果 SHALL NOT 重放，子 occurrence SHALL 保留在存档检查点并可在下一次恢复中继续

#### Scenario: A child occurrence completes before another child fails

- **WHEN** 同一链中前一个 occurrence 已完成而后一个 occurrence 暂时失败
- **THEN** 已完成 occurrence SHALL 从检查点按其独立序号消费，未完成 occurrence SHALL 保留且重试时不得重复前序效果

#### Scenario: A save contains multiple independent chains

- **WHEN** 加载后的存档包含多个合法的营地事件链检查点
- **THEN** Settlement Runner SHALL 按检查点顺序逐链恢复，并在最后一条完成前保持流程门禁

#### Scenario: A checkpoint references unavailable or overflowed content

- **WHEN** 恢复时稳定事件 ID 无法由当前内容目录解析，或检查点带有溢出诊断
- **THEN** 加载流程 SHALL 保留原始检查点、报告可诊断失败并保持流程门禁，且 SHALL NOT 静默删除或执行未持久化的分支

### Requirement: Save replacement preserves a recoverable snapshot

每次保存 SHALL 先把带 schemaVersion 与内容校验值的完整封套写入同目录临时文件并刷盘，再替换正式文件；已有正式文件 SHALL 保留为上一份备份。版本门禁 SHALL 覆盖异步保存、立即保存与删除。

#### Scenario: The process stops during a write

- **WHEN** 新快照尚未完成原子替换
- **THEN** 旧正式文件或上一份备份 SHALL 仍可作为完整候选读取

#### Scenario: The primary save is corrupt

- **WHEN** 文件头、封套、校验值或 Settlement JSON 验证失败
- **THEN** 继续战役 SHALL 尝试上一份备份，并只在主档和备份均无效时拒绝继续

#### Scenario: A legacy raw save is loaded

- **WHEN** 文件是可识别的旧版 SettlementInstance JSON 而非新封套
- **THEN** 读取 SHALL 保持兼容，下一次保存 SHALL 自动写为当前封套格式

### Requirement: Campaign deletion removes recovery artifacts

删除战役 SHALL 在同一个版本门禁中清理正式文件、备份与遗留临时文件，且更旧的后台保存 SHALL NOT 在删除后重新创建存档。

#### Scenario: A new campaign replaces an old one

- **WHEN** 玩家确认删除旧战役
- **THEN** 开场交互 SHALL 不再把任一旧候选识别为可继续的战役
