## MODIFIED Requirements

### Requirement: Hunt event occurrence restore is structurally complete

活动狩猎 occurrence 恢复 SHALL 在解析内容前拒绝零序号、重复序号、pending/committed 冲突以及重复 committed 序号。不得恢复 `int.MaxValue` 或 `int.MinValue` occurrence。`NextSequence` SHALL 至少为 1 且严格大于所有已观察正序号；`NextRootSequence` SHALL 至少为 -1，且在存在已观察负序号时严格小于所有已观察负序号。

恢复通过后 SHALL 保留输入顺序和全部 occurrence；相同 EventId 的 sibling SHALL NOT 因内容 ID 相同而去重。

#### Scenario: A malformed occurrence checkpoint is loaded

- **WHEN** pending 或 committed 序号为零、重复、相互冲突，或游标不能严格位于已观察序号之后
- **THEN** 恢复 SHALL fail closed
- **AND** SHALL NOT 返回部分恢复的 occurrence store

#### Scenario: Repeated sibling events are serialized

- **WHEN** 两个 pending occurrence 使用相同 EventId 但不同 sequence 并经过 JSON 往返
- **THEN** 恢复 SHALL 保留两项及其原始顺序
- **AND** 后续消费一项 SHALL NOT 隐式消费另一项
