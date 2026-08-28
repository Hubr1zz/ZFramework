---
schemaVersion: 2
category: system
title: "ZFramework 启动期内容源预载"
---

# ZFramework 启动期内容源预载 Delta

## MODIFIED Requirements

### Requirement: 游戏逻辑仅由最终启动流程进入

`ProcedureStartGame` SHALL 先通过 ZFramework Singleton 内容源系统异步加载并校验 Hunting in Darkness Manifest。只有内容源 Bundle 准备成功后，Procedure 才可调用无参数 `GameApp.Entrance`；只有 `GameApp.IsEntered` 为真时 Launcher UI 才可隐藏。加载或校验失败 SHALL 保持 Launcher 可见、释放已取得的资源租约并停止创建正式 GameManager。

#### Scenario: 启动内容源准备成功

- **WHEN** Procedure 状态机进入 `ProcedureStartGame` 且 Manifest 可加载并通过校验
- **THEN** 同一个内容源 Bundle SHALL 在 `GameApp.Entrance` 前可用
- **AND** 游戏逻辑成功进入后 Launcher UI SHALL 被隐藏

#### Scenario: 启动内容源准备失败

- **WHEN** Manifest 缺失、schema 不受支持或必需内容引用无效
- **THEN** `GameApp.Entrance` SHALL NOT 创建正式 GameManager
- **AND** Launcher UI SHALL 保持可见
