---
name: project-gamecore
description: Hunting in Darkness GameCore / Adapters / ViewLayer 分层规则。用于战斗、棋盘、规则层、Unity 适配、纯 C# 领域逻辑相关任务；迁移到新项目时可替换。
---

# Project GameCore

这是项目内容层 skill，描述个人项目的 GameCore 分层。

## 必读参考

- [GAMECORE.md](references/GAMECORE.md)

## 核心原则

- GameCore 保持纯 C#，不引用 UnityEngine。
- Adapters/Unity 负责 SO、UniTask、EventBus、Unity 坐标和兼容 API。
- ViewLayer 负责输入、动画、3D/uGUI 表现。
- 依赖只能从 ViewLayer 指向 Adapters，再指向 GameCore。
