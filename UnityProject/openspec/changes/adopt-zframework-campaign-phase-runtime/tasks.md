## 1. ZFramework 阶段运行态

- [x] 1.1 新增 Campaign Runtime Module 与独占代际 lease
- [x] 1.2 将阶段 FSM 创建、切换、重置和释放迁出 GameManager
- [x] 1.3 将共享 ActionEnvironment registry、发明租约和 Campaign Runner 迁入 runtime scope
- [x] 1.4 移除玩家阶段切换与遭遇命令的 ActionQueue 旁路
- [x] 1.5 保持存档、阶段 Session 和 3D 表现边界

## 2. 验证

- [x] 2.1 Unity CLI 编译通过
- [x] 2.2 ActionEnvironment/Campaign Runner EditMode 13/13 通过
- [x] 2.3 Campaign Runtime lease 定向 PlayMode 4/4 通过
- [x] 2.4 非决战 GameManager 战役循环 PlayMode 12/12 通过
- [ ] 2.5 人类审查本 Delta Spec 后批准同步到正式 Spec
