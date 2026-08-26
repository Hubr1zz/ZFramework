# 设计

`PlayableHuntEventOccurrenceStore.TryRestore` 先验证 committed 序号唯一且非零，再验证 pending 序号唯一、非零且不与 committed 相交；`int.MaxValue`/`int.MinValue` occurrence 直接拒绝。`NextSequence` 必须大于全部已观察正序号；`NextRootSequence` 至少为 -1，并在存在已观察负序号时严格小于全部已观察负序号。通过后按输入顺序解析并重建，最终比较 pending 与运行时 occurrence 数量，避免队列构造器静默丢项。

校验按序号而非 EventId 去重，因此同一 EventId 的 sibling 仍以独立 sequence 保留。正式生产 `rust_burial` 子链已通过现有 Hunt ActionSession 与 Hunt collectibles 资源端口完成恢复消费验证；本 Change 不新增 runtime seam。
