## 1. 收窄 Settlement/Campaign 入口

- [x] 1.1 移除 `ISettlementDepartureRequestPort`、`SettlementManager.TryDepart` 及 runtime/phase 注入
- [x] 1.2 移除 GameManager 旧成长/出猎公开 facade，保留 typed 3D departure chain
- [x] 1.3 更新 runtime module/loop 测试，断言旧接口和公开旁路不存在

## 2. 删除旧表现旁路

- [x] 2.1 删除症状 screen-space Service/View 及其 meta
- [x] 2.2 删除三个无绑定 IMGUI Toast 及其 meta
- [x] 2.3 保留 3D Settlement panels、SettlementNoticePresenter3D 与 after-commit 事实路径

## 3. 验证与对账

- [x] 3.1 核验删除脚本的 C# 引用和 Unity 序列化 GUID
- [x] 3.2 核验 formal 3D table callback → destination input → typed transaction 未被移除
- [x] 3.3 记录 Unity 编译通过和 EditMode 71/71
- [ ] 3.4 在 Unity license handshake 可用后补跑 PlayMode 定向回归
