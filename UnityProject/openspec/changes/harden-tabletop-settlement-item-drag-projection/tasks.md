## 1. 3D 投影

- [x] 1.1 让仓库、装备与临时使用卡统一建立并清理 Slot/Card 双向关系
- [x] 1.2 保持消耗品使用槽为不占位的临时命令目标

## 2. 异步命令门禁

- [x] 2.1 锁定装备/卸装 pending 期间的重复拖拽与分页
- [x] 2.2 分离命令 token 与展示 generation，覆盖隐藏、重开和重绑时序
- [x] 2.3 在成功、失败与 prevent 后从权威状态收敛并恢复交互

## 3. 验证

- [x] 3.1 增加 SlotGrid 清理、transient use target 与跨展示 pending 的窄 PlayMode 回归
- [x] 3.2 增加正式 GameManager 组合根下装备和消耗品 3D 拖拽回归
- [x] 3.3 运行定向 Unity CLI、编译、OpenSpec strict validation 与 diff 验证
