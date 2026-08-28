## 1. Campaign 保存状态

- [x] 1.1 增加只读保存状态与 revision/generation 门禁
- [x] 1.2 保持关键保存调用结果独立，并隔离旧战役迟到完成
- [x] 1.3 实现重新捕获最新快照的单飞重试

## 2. 3D 恢复入口

- [x] 2.1 通过窄 Campaign 读写端口公开状态与重试命令
- [x] 2.2 在营地/狩猎当前根节点显示持久失败卡和重试卡
- [x] 2.3 保持存档 I/O 与 UI 交互在 ActionQueue 之外

## 3. 验证

- [x] 3.1 使用 Unity 6000.5.9f1 完成编译
- [x] 3.2 完成 CampaignPersistenceCoordinator EditMode 10/10
- [x] 3.3 完成 CampaignSaveStatusPresenter3D PlayMode 2/2
- [x] 3.4 完成并发保存、取消、Reset、重试与 View 生命周期对抗审查
