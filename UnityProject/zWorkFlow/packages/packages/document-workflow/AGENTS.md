# Document Workflow

处理文档前读取 `.agents/skills/document-maintenance/SKILL.md`。

文档包可独立工作。成功修改正式设计文档后，按 `document-maintenance` 更新 `.design-workflow/implementation-ledger.json` 中已经登记实现基线的对应条目。

不得读取或调用项目目录中的工作流，不得自动生成 OpenSpec proposal 或 apply 项目代码。项目包是否读取变更账本不影响文档维护。
