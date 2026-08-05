# Design Source Configuration

`openspec/design-source.json` 保存本机设计文档路径，不进入 Git。schema v2 支持多个等价来源：

```json
{
  "schemaVersion": 2,
  "sources": [
    { "id": "primary", "path": "D:/DesignVault/设计文档" },
    { "id": "7af0cbe31bf14b1da66c080d9fa26ec2", "path": "D:/DesignVault/内容记录" }
  ],
  "configuredBy": "nickname",
  "configuredAt": "2026-07-23T17:30:00+08:00"
}
```

## 约束

- 所有路径都是等价的正式设计来源，不为路径声明规则、内容或美术角色。
- `id` 在配置内唯一且稳定，只允许字母、数字、点、下划线和连字符。更新路径时保留 ID；删除后重新添加视为新来源。
- 路径必须唯一。允许绝对路径；相对路径按项目根目录解析。
- schema v1 的单个 `source` 自动迁移为 ID 为 `primary` 的来源。
- 显式 `--source [ID=]PATH` 可重复并只覆盖本次扫描；未指定时读取配置中的全部来源。

## 扫描与引用

- 先在全部来源中按 scope、相对路径、标题与直接 Wiki 链接收敛候选，再逐句执行规则、内容、美术语义过滤。类型参数不得决定扫描哪个路径。
- 跨路径扫描只先枚举文件名、标题和 `rg` 命中，不得把所有来源全文加载进 Agent 上下文；只读取收敛后的候选及其直接上下文，以控制 token。
- 同一来源内优先解析 Wiki 链接；需要跨来源解析时，只有唯一匹配才自动加入上下文。同名候选超过一个时写入导入报告，禁止静默选择。
- `sources.json` 中每条记录保存 `sourceId`、`relativePath`、角色和 hash。
- 新生成的来源引用使用 `<source-id>::<relative-path>:<line>`，例如 `primary::Systems/Ability.md:24`。旧的 `<relative-path>:<line>` 引用继续兼容，但跨来源同名时可能解析为多个文件。
- 重复预检以 `sourceId + relativePath + sha256` 识别来源，不能只用文件名或相对路径。
