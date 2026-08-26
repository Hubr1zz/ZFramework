## 1. Settlement phase ownership

- [x] 新增 coordinator 并由 SettlementPhaseManager 创建、重置、释放。
- [x] 将 Settlement ActionSession 激活/停用转入 coordinator，保留 runtime 兼容 API。
- [x] 将 SettlementTable3D fallback 初始化和阶段命令回调绑定转入 coordinator。
- [x] 补充当前 generation 与旧 View 回调的 PlayMode 覆盖。

## 2. Verification

- [x] Unity EditMode Settlement action tests。
- [x] Unity PlayMode campaign runtime、campaign loop 与 production event tests。
- [x] dotnet build、OpenSpec strict validate 与 diff check。
- [ ] 人类审查本 Delta Spec 后批准同步到正式 Spec。
