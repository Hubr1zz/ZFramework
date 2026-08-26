# 提案：加固狩猎事件 occurrence 恢复

活动狩猎快照目前依赖队列构造器静默丢弃非法序号，可能把损坏的 pending/committed 状态误恢复为部分事件链。恢复边界应在解析内容前拒绝结构不一致，同时保留重复 EventId sibling 的独立 occurrence 语义。

本 Change 只收敛 occurrence 序号、游标和 JSON 往返校验，不改变事件效果、ActionQueue、遭遇或 3D 表现。
