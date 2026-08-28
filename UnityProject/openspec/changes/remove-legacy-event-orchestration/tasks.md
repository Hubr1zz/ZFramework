## 1. 事件执行权威

- [x] 1.1 删除 EventSystem 共享队列、隐式 actor、UI callback 与继续 API
- [x] 1.2 保留显式 context 的 narrative、choice、reroll 与 effect resolver
- [x] 1.3 调整测试，不再依赖旧队列结算入口

## 2. 3D 生产表现

- [x] 2.1 从 Settlement 组合根与 phase port 移除兼容 HUD 依赖
- [x] 2.2 删除零序列化引用的 Settlement HUD、猎人详情与出发确认脚本
- [x] 2.3 将 Hunt presenter 收窄为 3D 状态板/采集面板 owner，删除 screen-space fallback

## 3. 验证

- [x] 3.1 审计删除脚本的 C# 动态引用与 Unity GUID 序列化引用
- [x] 3.2 使用 Unity 6000.5.9f1 完成编译
- [x] 3.3 完成事件 resolver/HUD 边界 EditMode 33/33
- [x] 3.4 完成事件桌面 PlayMode 2/2 与战役循环/3D 采集 smoke 2/2
- [x] 3.5 完成无 3D 根、重绑、Missing Script 与旁路 API 的对抗审查
