---
name: wiki-sync
description: 同步 ZFramework 项目 Wiki 与代码事实。用于用户要求同步 repowiki、修正文档与代码不一致或更新 Wiki 导航时。
---

# ZFramework Wiki Sync

1. 根据用户范围或当前改动确定模块；未指定时只覆盖本次代码改动影响范围。
2. 先用 `codebase-query` 收敛 C# 候选，失败时回退 `rg`，再读取源码核验。
3. 读取 `repowiki/zh/content/index.md` 和命中的 Wiki 页面，并按需读取 `.agents/skills/zframework-dev/references/`。
4. 以代码事实为准修正、补充或删除过时说明，同时维护交叉引用和 Wiki 索引。
5. 输出已核验代码、更新文档、关键对齐点和未处理风险。

完整项目资料与规则只保存在 `.agents/` 和 `repowiki/`，不得写入 `.claude` 私有 memory。
