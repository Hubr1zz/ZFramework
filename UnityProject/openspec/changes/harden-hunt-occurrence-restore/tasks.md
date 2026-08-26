## Implementation

- [x] 在 `PlayableHuntEventOccurrenceStore.TryRestore` 增加序号、游标、重建数量的 fail-closed 校验。
- [x] 在 `ActiveHuntPersistenceTests` 覆盖重复 sibling JSON 顺序、重复 pending、pending/committed 冲突及非法边界。
- [x] 保留负 root occurrence 的成功恢复回归。
- [x] 使用正式 `rust_burial → open_eyes`、Hunt ActionSession 和资源端口补完整恢复消费证据；不为此新增 runtime seam。
- [x] 不修改 3D 表现与交互；未宣称人工 3D 覆盖。
