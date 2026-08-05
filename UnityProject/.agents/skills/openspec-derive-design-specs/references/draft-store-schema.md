# Change-only Draft Store

此文件保留旧链接兼容。权威结构见 [change-schema.md](change-schema.md)。

- `openspec/drafts/changes/<change-id>/` 是唯一 Draft 内容存储。
- `openspec/drafts/index.json` 只可保存工作台索引，不得复制 Spec/Review 正文。
- `draft-refs.json` 使用 `{ capability, changeId, status }`；规则与实现可指向同一个配对 Change。
- `openspec/drafts/specs/` 已废止。迁移时必须先验证每份内容在 Draft Change 中有等价副本，再删除旧目录。
