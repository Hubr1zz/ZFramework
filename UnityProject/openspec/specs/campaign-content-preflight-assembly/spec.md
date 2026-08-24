---
schemaVersion: 2
category: architecture
title: "战役内容启动预检装配"
---

# Campaign Content Preflight Assembly Specification

## Purpose

在正式 `GameManager` 创建前冻结并验证非 Showdown 战役内容，使无效营地或狩猎目录不能通过旧测试回退伪装成可玩启动。

## Requirements

### Requirement: Build is side-effect free and fail-closed

候选构建 SHALL 只读取启动配置并生成结构化诊断，不得修改已安装 Runtime。缺失必需目录、重复目的地稳定 ID、最早可用年份无狩猎内容时 SHALL 拒绝候选。

#### Scenario: Invalid settings are submitted

- **WHEN** 启动配置为空或包含无效目的地
- **THEN** 构建 SHALL 返回错误诊断
- **AND** 当前 Runtime 引用 SHALL 保持不变

### Requirement: Candidate freezes validated references

候选 SHALL 在构建时捕获安装需要的 Catalog、目的地集合、阶段、尺寸与遭遇配置。构建完成后修改来源 Settings SHALL NOT 改变候选。

#### Scenario: Settings drift after build

- **WHEN** 来源 Settings 的 Settlement 或 Hunt 引用被替换
- **THEN** 候选 SHALL 继续持有构建时验证的引用

### Requirement: Installed content is probed before GameManager activation

正式 Bootstrap SHALL 先安装候选，并在隔离的 `SettlementManager` 上执行真实内容投影。只有营地初始猎人、稳定身份与跨表规则均通过后，才可创建并激活 `GameManager`。

#### Scenario: Settlement content projection fails

- **WHEN** 正式营地目录无法应用到隔离状态
- **THEN** Bootstrap SHALL 输出结构化错误并停止
- **AND** SHALL NOT 创建使用旧测试猎人回退的正式 GameManager

### Requirement: Startup installation has a process gate

同一候选的已提交安装 SHALL 幂等返回成功；不同候选或失败后的安装 SHALL 在任何新一轮写入前被拒绝。安装过程 SHALL 捕获异常并返回诊断。

#### Scenario: Another content generation attempts installation

- **WHEN** 当前进程已经提交内容候选
- **THEN** 不同候选 SHALL 被安装门禁拒绝

## Known Boundary

当前门禁保证失败内容不会进入玩家运行态，但各兼容静态 Runtime 的安装仍是启动期顺序写入；完整 capture/rollback 与单一不可变上下文指针属于后续原子安装事务阶段。
