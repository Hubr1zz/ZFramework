## 1. Campaign 重启命令

- [x] 1.1 增加 typed `CampaignRestartResult`、Restart Action 与 committed fact
- [x] 1.2 由 ZFramework Campaign Runtime 转发到当前 Campaign ActionSession
- [x] 1.3 Before Reactor 可阻止重启且不触碰宿主

## 2. 可回滚重启事务

- [x] 2.1 等待可靠删除并先写入候选稳定快照
- [x] 2.2 以 Settlement/Hunt generation CAS 和阶段归位发布新战役
- [x] 2.3 失败/取消释放候选并恢复旧稳定载荷
- [x] 2.4 将存档与 generation 事务从 GameManager 下沉到独立 Core 生命周期服务

## 3. 世界空间交互与验证

- [x] 3.1 终局卡失败时保留并显示原因，成功后关闭
- [x] 3.2 Campaign runner EditMode 8/8、GameManager PlayMode 14/14、终局 View PlayMode 1/1
- [ ] 3.3 人类审查 Delta Spec 后批准同步到正式 Spec
