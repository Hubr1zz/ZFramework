# Project Content Template

本模板只在项目没有等价能力、目标路径未被占用且用户当前任务确实需要时使用。它用于创建 zWorkFlow 自有的最小项目上下文；不得覆盖或改写已有同类文档。若已有能力足够，在能力映射中标记 `reuse-existing`，不要生成本模板。

## project-context/SKILL.md

~~~markdown
---
name: project-context
description: <项目名> 项目速查。用于理解项目类型、目录、模块、运行方式、已知限制和开发约束。
---

# Project Context

## 必读参考

- [PROJECT-INDEX.md](references/PROJECT-INDEX.md)：项目事实的低成本路由索引

## 项目概述

- 项目名：
- 类型 / 技术栈：
- 主要入口：
- 运行方式：
- 验证方式：

## 目录结构

| 路径 | 职责 |
| --- | --- |

## 核心模块

| 模块 | 路径 | 职责 | 主要依赖 |
| --- | --- | --- | --- |

## 开发约束

-

## 已知限制

-

## 缺失 / 待确认

-
~~~

## project-context/references/PROJECT-INDEX.md

~~~markdown
# Project Index

只列已确认的事实来源。Agent 先用本索引选择相关文档或领域 skill，不默认读取整个项目。

| 任务 / 领域 | 首选来源 | 何时读取 |
| --- | --- | --- |
| 项目整体 | `../SKILL.md` | 需要目录、入口或模块概览时 |
| 架构边界 | `../../project-architecture/SKILL.md` | 架构设计、重构或新增模块时 |
| Plugin / Architecture / System 工程能力 | `../../project-tooling/SKILL.md` | 任务涉及序列化、动画、资源、启动、依赖注入、异步或编辑器扩展时 |
| 领域内容 | `../../project-domain-<domain>/SKILL.md` | 任务命中该领域时 |
| 项目代码流程 | `CODE-WORKFLOW.md` | setup 从已有项目入口迁入代码分级、编码红线或强制实现流程时 |
~~~

## project-context/references/CODE-WORKFLOW.md（按需）

只有已有 `CLAUDE.md`、工具 Skill 或项目文档确实定义了代码任务强制流程时才生成。提取任务分级、必读资料、编码红线、源码冲突处理和验证要求，更新所有工具私有路径为 `.agents/` 项目相对路径；不得复制工具模型、命令语法、凭据、私有 memory 或机器绝对路径。

生成后，根薄入口和 `PROJECT-INDEX.md` 必须明确要求“生成或修改项目代码”先读取本文件。原工具入口只有在内容迁入和引用校验通过后才允许薄化。

## project-architecture/SKILL.md

~~~markdown
---
name: project-architecture
description: <项目名> 架构边界与依赖规则。用于架构设计、重构、模块拆分和新增功能落点判断。
---

# Project Architecture

## 分层 / 边界

```text
<表现层 / 接口层>
        ↓
<应用层 / 适配层>
        ↓
<领域层 / 核心规则>
        ↓
<基础设施>
```

## 依赖规则

-

## 数据流

-

## 扩展规则

-

## 风险与缺口

-
~~~

## project-domain-*/SKILL.md

~~~markdown
---
name: project-domain-<domain>
description: <领域名> 领域说明。用于该领域的新功能、修复、重构和文档同步。
---

# <Domain>

## 职责

-

## 关键文件

| 路径 | 职责 |
| --- | --- |

## 核心流程

```text
<流程图或步骤>
```

## 事件 / API / 数据

-

## 常见风险

-
~~~

## project-refactor-queue/SKILL.md

~~~markdown
---
name: project-refactor-queue
description: 项目重构工作台。用于独立读取保护清单，并在增量重构或技术债维护时管理待处理项。
---

# Project Refactor Queue

## 必读参考

- [PROTECTED_FILES.md](references/PROTECTED_FILES.md)：仅在可能修改项目文件时读取
- [REFACTOR_QUEUE.md](references/REFACTOR_QUEUE.md)：仅在增量重构、技术债维护或用户查看队列时读取

两个 reference 分别是保护清单和待处理队列的唯一数据源。工具专属目录只能保存薄转接口。
~~~

## project-refactor-queue/references/PROTECTED_FILES.md

~~~markdown
# Protected Files

<!-- 示例：- path/to/protected-file.ext -->
~~~

## project-refactor-queue/references/REFACTOR_QUEUE.md

~~~markdown
# Project Refactor Queue

## 待处理队列

### [优先级: 中] 简短标题
- **文件**:
- **类型**:
- **描述**:
- **来源**:
- **状态**: 待处理
- **维护人**: 未注明
- **维护时间**: 未维护
- **维护备注**: -

~~~

## project-doc-sync/SKILL.md

~~~markdown
---
name: project-doc-sync
description: 项目文档同步。用于代码改动后同步 README、docs、ADR、OpenSpec 或其他项目文档。
---

# Project Doc Sync

## 文档位置

-

## 同步规则

-

## 输出格式

文档同步完成。
- 核验范围：
- 更新文档：
- 关键对齐点：
- 未处理风险：
~~~
