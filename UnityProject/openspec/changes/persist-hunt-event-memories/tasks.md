## 1. 事件记忆契约

- [x] 1.1 收敛 Settlement/Hunt 共用的事件记忆 DTO、验证、深拷贝与等价判断
- [x] 1.2 在 Hunt Resolution checkpoint 写入稳定 occurrence 记忆并保持父子恢复幂等
- [x] 1.3 完整记录 FatalInjury 的死亡牌、部位、生命、永久损伤与死亡事实

## 2. 存档与回营

- [x] 2.1 将 ActiveHunt 升级为 v4，并对旧版、未来版本、超限和跨远征记忆 fail closed
- [x] 2.2 将 HuntReturn 升级为 v4，以 ExpeditionId 传递并验证有界事件记忆
- [x] 2.3 深拷贝记忆进入 HuntHistory，并保持相同 RecordId 的回营重试幂等

## 3. 3D 年鉴与验证

- [x] 3.1 在 3D 年鉴中按远征分组和 occurrence 顺序显示事件子条目
- [x] 3.2 为 FatalInjury 提供玩家可读且不泄露技术字段的年鉴文案
- [x] 3.3 通过 PlayableHunt、ActiveHuntPersistence、PlayableSettlementActionSession 与 SettlementTimelineJournal 定向测试
