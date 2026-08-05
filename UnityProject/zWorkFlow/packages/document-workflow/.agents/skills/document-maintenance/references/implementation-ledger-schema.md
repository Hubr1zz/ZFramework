# Implementation Ledger Schema

`.design-workflow/implementation-ledger.json` 是设计包拥有的审计索引，用于记录正式设计的实现基线，以及该设计是否在实现完成后再次发生实质变更。它不得保存项目绝对路径或触发目标工作流。

```json
{
  "schemaVersion": 1,
  "updatedAt": "",
  "entries": [
    {
      "documentPath": "设计文档/战斗.md",
      "implementationId": "combat-v1",
      "implementationLabel": "战斗系统首版",
      "implementationStatus": "implemented",
      "implementationProgress": 100,
      "implementedAt": "2026-07-31T12:00:00+08:00",
      "implementedRevision": "git-sha-or-empty",
      "implementedFingerprint": "sha256",
      "previousImplementedFingerprint": "",
      "currentRevision": "git-sha-or-empty",
      "currentFingerprint": "sha256",
      "changedAfterImplementation": false,
      "changedAt": "",
      "changeSummary": ""
    }
  ]
}
```

- `documentPath`：相对设计包根目录的稳定路径，使用 `/`。
- `implementationId`：同一设计存在多次或多个实现时的稳定标识，不包含机器路径。
- `implementationStatus`：`not-implemented | in-progress | implemented`；缺失时读取端按进度与实现基线推断。
- `implementationProgress`：0-100 的整数。建立实现基线时固定为 100；没有条目的文档由项目端显示为 0%。
- `implemented*`：最近一次确认实现完成时的基线。
- `current*`：最近一次由文档工作流核验的文档状态。
- `changedAfterImplementation`：只有实现基线之后发生实质设计变化时才为 true。
- `changeSummary`：只保存必要摘要，不复制设计正文。
- 手动绕过文档工作流的修改可能没有摘要。项目读取端发现当前指纹与账本 `currentFingerprint` 不一致时显示“手动修改”，但不写回账本。

指纹算法：以 UTF-8 读取文本，把 CRLF 归一化为 LF，去除文本首尾空白后计算小写十六进制 SHA-256。这样换行风格和文件首尾空白不会被误判为实质设计变化。

以 `(documentPath, implementationId)` 作为唯一键。读取端容忍未知字段；写入端使用 UTF-8、稳定排序和 ISO 8601 时间。
