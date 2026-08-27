---
schemaVersion: 2
category: architecture
title: "Player 构建与 ZFramework 启动入口"
---

# Player Build Bootstrap Specification

## Purpose

保证普通 Player 构建包含唯一可启动场景，并通过 ZFramework 模块与 Procedure 生命周期进入可游玩业务，而不是只在 Editor 测试环境中成立。

## Requirements

### Requirement: Player build contains the ZFramework bootstrap scene

`Assets/Scenes/main.unity` SHALL 作为启用的 Player 首场景进入 `EditorBuildSettings`。该场景 SHALL 装配 `GameEntry` 与 `UIRoot`，不得依赖测试场景或编辑器当前打开场景启动。

#### Scenario: A Windows player is built from project settings

- **WHEN** Unity CLI 使用 `StandaloneWindows64` 构建 Player
- **THEN** 构建 SHALL 包含 `Assets/Scenes/main.unity` 作为首场景
- **AND** Player SHALL NOT 因空场景列表而生成无启动入口的包

### Requirement: Module discovery follows the interface naming contract

由 `ModuleSystem.GetModule<T>()` 自动发现的模块实现 SHALL 位于接口程序集和命名空间内，并使用接口名去掉前导 `I` 后的类型名。`IUpdateDriver` SHALL 解析为 `ZFramework.UpdateDriver`，不得通过项目侧平行 MonoBehaviour 绕过框架模块生命周期。

#### Scenario: GameEntry requests the update driver

- **WHEN** Player 首场景执行 `GameEntry.Awake`
- **THEN** `ModuleSystem.GetModule<IUpdateDriver>()` SHALL 创建并初始化 `ZFramework.UpdateDriver`
- **AND** 启动 SHALL 继续进入资源初始化 Procedure

### Requirement: Built player reaches the game extension point

发布前的启动烟雾验证 SHALL 使用实际图形设备运行临时 Player，并通过日志确认资源包初始化、`GameApp.Entrance`、可游玩 Bootstrap 和正式开场等待状态均已到达。验证 SHALL NOT 依赖截图或 Showdown 流程。

#### Scenario: A built player starts normally

- **WHEN** 临时 Windows Player 在实际图形设备上启动
- **THEN** 日志 SHALL 到达 `Entrance Hunting in Darkness`
- **AND** Settlement、Hunt、BossFight 阶段根 SHALL 完成装配
- **AND** 正式开场菜单 SHALL 等待玩家选择且日志中没有启动异常

## Implementation evidence

- `ProjectSettings/EditorBuildSettings.asset` enables `Assets/Scenes/main.unity` as the Player bootstrap scene.
- `Assets/ZFramework/Runtime/Module/UpdataDriverModule/UpdateDriverModule.cs` provides the convention-compatible `ZFramework.UpdateDriver` implementation.
- Unity CLI 6000.5.9f1 successfully built a `StandaloneWindows64` Player and an actual-graphics startup smoke reached `GameApp.Entrance` without exceptions.
