## 1. 稳定内容与持久数据

- [x] 1.1 为营地事件选项增加稳定 `optionId` 并校验正式表格与 ScriptableObject 内容
- [x] 1.2 增加事件记忆、Timeline 根链接、schema 迁移和深拷贝幂等提交

## 2. 权威提交与展示

- [x] 2.1 从 ActionQueue Resolution checkpoint 提交根、子链和触发事件记忆
- [x] 2.2 保证子链恢复容量失败时父事件的效果、Timeline、记忆和提交事实一致
- [x] 2.3 在 3D 年鉴中展示玩家/自动选择、判定、结果与结构化效果

## 3. 验证

- [x] 3.1 覆盖旧档、JSON round-trip、幂等/冲突、重复 occurrence 与子链上限
- [x] 3.2 通过相关 EditMode 50/50、正式营地生产 PlayMode 2/2 和解决方案编译
