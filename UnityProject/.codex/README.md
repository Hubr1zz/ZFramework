# Codex Layer

Codex 的完整 repo skills 位于 `.agents/skills/`，这里不维护 `.codex/skills/` 副本。

本目录只保留 Codex 专用配置或 `.codex/agents/*.toml` 壳层。新增或修改通用能力时，先改 `.agents/`。

## 默认双 Agent 路由

- `solution-architect`：`gpt-5.6-sol` + `medium`，负责代码规划、方案设计、OpenSpec 与宏观边界。
- `code-implementer`：`gpt-5.6-luna` + `high`，负责定向阅读已有代码、命令/工具输入输出、实现和验证。

主 Agent 应先让 `code-implementer` 形成最小事实包，再交给 `solution-architect` 设计；只有任务足够复杂且两者不会重复读取时才同时启用。Unity 验证默认使用 `Tools/UnityCli/Invoke-Unity.ps1`，不使用 Unity MCP。
