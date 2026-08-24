---
schemaVersion: 2
category: architecture
title: "战役内容缓存生命周期"
---

# Campaign Content Cache Lifecycle Specification

## Purpose

为战役内容原子装配提供明确的临时对象所有权与重建边界，使 Settlement 与 Hunt 消费同一批事件对象，并避免开发期重复构建泄漏表运行时生成的 `ScriptableObject`。

## Requirements

### Requirement: Table runtime owns only the events it creates

事件表 Runtime SHALL 跟踪由表记录创建的 transient `EventData`，清理缓存时 SHALL 只销毁这些对象，不得销毁 Catalog 或场景提供的外部事件资产。

#### Scenario: An event cache is cleared

- **WHEN** 已构建的事件表缓存被显式清理
- **THEN** 所有由该缓存创建的事件对象 SHALL 被释放
- **AND** 外部传入的事件资产 SHALL 保持有效

### Requirement: Rebuild publishes a complete event generation before retirement

显式重建 SHALL 先离线构建一套完整的新事件与事件链引用，再以单一交换点发布新世代，最后释放旧 transient 事件；构建或发布前失败 SHALL 保留旧世代，不得混用不同批次的对象。

#### Scenario: Content is rebuilt from another record set

- **WHEN** 当前世代 A 成功从记录 B 构建替代世代
- **THEN** 查询只返回 B 创建的新对象
- **AND** 旧 A 对象不再由 Runtime 持有

#### Scenario: A staged generation is rejected

- **WHEN** 新世代已经构建但后续内容投影失败
- **THEN** Runtime SHALL 恢复并继续返回原世代的相同 EventData 引用
- **AND** SHALL 只释放被拒绝世代拥有的 transient 对象

### Requirement: Event dependency identity is frozen per generation

事件世代 SHALL 捕获其症状目录与血脉内容依赖。普通查询 SHALL 继续返回当前已发布世代，不得因依赖引用改变而隐式销毁仍被 Registry、Timeline 或 Runner 消费的 EventData；依赖变化 SHALL 只在显式重建或启动事务中生效。

#### Scenario: Bloodline content is replaced

- **WHEN** 当前血脉内容 Provider 被另一实例替换
- **THEN** 普通事件查询 SHALL 保持当前世代及其对象身份
- **AND** 显式重建 SHALL 返回基于新 Provider 构建的新世代

### Requirement: Production rebuild occurs before gameplay activation

正式 Bootstrap SHALL 在所有事件依赖目录配置完成后、创建并激活 `GameManager` 前重建共享事件缓存。运行中的 Settlement/Hunt Catalog 应只消费该批缓存，不得隐式触发热重载。

#### Scenario: The playable composition root starts

- **WHEN** 症状、血脉、生存事件及其他依赖目录已经配置
- **THEN** Bootstrap SHALL 构建一次共享事件对象图
- **AND** Settlement 与后续 Hunt SHALL 复用该批对象
