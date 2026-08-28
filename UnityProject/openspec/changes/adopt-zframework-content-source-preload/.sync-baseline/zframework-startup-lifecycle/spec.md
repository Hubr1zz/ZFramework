---
schemaVersion: 2
category: system
title: ZFramework 启动生命周期
---

# ZFramework 启动生命周期

## Purpose

定义本项目从 Unity 场景入口初始化 ZFramework 模块、执行资源准备流程并进入游戏逻辑入口的稳定调度边界。

## Requirements

### Requirement: 场景入口启动框架流程

`GameEntry` SHALL 在 Unity `Awake` 阶段取得更新、资源、调试和状态机模块，随后启动配置的首个 Procedure，并保持入口对象跨场景存活。

#### Scenario: 启动场景加载

- **WHEN** 包含 `GameEntry` 的启动场景进入运行态
- **THEN** 必需框架模块完成注册访问，配置的首个 Procedure 开始执行

### Requirement: 资源准备按运行模式分支

资源初始化 Procedure SHALL 请求包版本并更新资源清单。Host/Web 模式 SHALL 按在线更新条件进入下载链或边玩边下载分支；EditorSimulate/Offline 模式 SHALL 直接进入内容包初始化扩展点。

#### Scenario: 本地资源模式完成初始化

- **WHEN** 资源清单更新成功且运行模式为 EditorSimulateMode 或 OfflinePlayMode
- **THEN** 流程进入 `ProcedureInitContentPackages`

#### Scenario: 联机资源模式需要完整下载

- **WHEN** 运行模式为 HostPlayMode 且未启用边玩边下载
- **THEN** 流程进入创建下载器、下载、下载完成和缓存清理链，再进入内容包初始化

### Requirement: 游戏逻辑仅由最终启动流程进入

`ProcedureStartGame` SHALL 调用 `GameApp.Entrance`，由其初始化游戏事件、注册销毁释放回调并启动首个游戏逻辑界面；随后启动器 UI SHALL 被隐藏。

#### Scenario: 启动流程完成

- **WHEN** Procedure 状态机进入 `ProcedureStartGame`
- **THEN** `GameApp.Entrance` 执行，游戏逻辑启动并关闭启动器 UI
