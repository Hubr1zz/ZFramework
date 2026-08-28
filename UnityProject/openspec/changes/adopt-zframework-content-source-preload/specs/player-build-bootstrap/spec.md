---
schemaVersion: 2
category: architecture
title: "Player 构建内容源启动入口"
---

# Player 构建内容源启动入口 Delta

## MODIFIED Requirements

### Requirement: Built player reaches the game extension point

发布前的启动烟雾验证 SHALL 使用实际图形设备运行临时 Player，并通过日志确认资源包初始化、Hunting in Darkness Manifest 预载、`GameApp.Entrance`、可游玩 Bootstrap 和正式开场等待状态均已到达。内容源资产 SHALL 由 YooAsset 收集而非 Unity Resources 文件夹隐式包含。验证 SHALL NOT 依赖截图或 Showdown 流程。

#### Scenario: A built player starts normally

- **WHEN** 临时 Windows Player 在实际图形设备上启动
- **THEN** 日志 SHALL 先确认内容源 Manifest 准备成功，再到达 `Entrance Hunting in Darkness`
- **AND** Settlement、Hunt、BossFight 阶段根 SHALL 完成装配
- **AND** 正式开场菜单 SHALL 等待玩家选择且日志中没有启动异常
