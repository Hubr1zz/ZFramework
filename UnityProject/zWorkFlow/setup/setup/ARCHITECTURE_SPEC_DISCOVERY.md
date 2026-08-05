# System Spec Discovery（兼容旧文件名）

本流程负责从项目已有的系统描述与代码事实中补充 `system` 正式 Spec。它只处理与具体项目领域、Manager、阶段或跨系统契约耦合的 System；可脱离当前游戏独立复用的 Architecture 与第三方 Plugin 进入 `project-tooling` 工程能力目录。文件名为旧版兼容保留。

## 触发条件

仅在以下情况执行：

- 用户要求 setup、检测项目架构或导入架构资料。
- 用户先导入 zWorkFlow，之后又导入或新增架构文档并要求检测。
- 用户先提供架构资料，之后安装 zWorkFlow 并要求 setup。

普通问答、灵感记录、非架构文档修改不得自动触发完整扫描。

## 识别架构级描述

只读扫描项目内部的 README、docs、ADR、模块说明、已有 Spec 和 skill。满足下列任一特征的内容可作为候选：

- 定义模块、程序集、包或目录的依赖方向。
- 定义组合根、全局服务、Manager、Coordinator、状态机或跨系统调度边界。
- 定义资源、UI、事件、配置、数据流、生成代码或生命周期的全局约束。
- 定义多个子系统必须共同遵守的接口、禁用项或验证方式。

普通业务规则、单一功能实现、局部编码偏好和示例代码不属于 System Spec。

## 代码交叉核验

1. 从候选文档提取架构声明、来源位置与可能的 capability。
2. 用包清单、构建配置、程序集、模块入口、依赖引用和关键类型检查项目是否实际采用该结构。
3. 将结果分为：
   - `confirmed`：文档与代码一致，可形成正式 Requirement。
   - `partial`：只确认部分边界，仅写已证实部分，其余留在 Verification。
   - `stale-or-conflicting`：代码与描述明显不符，不生成正式 Requirement，写入报告等待用户处理。
4. 不为了匹配文档而修改代码，也不为了匹配代码而修改来源文档。

## 生成 System Spec

- 按稳定项目系统边界聚合 capability，不按类逐个生成。
- 写入新的 `openspec/specs/<capability>/spec.md` 与 `spec-review.json`，分类固定为 `system`；读取 legacy `architecture` 时归一化为 `system`。
- System Spec 只允许依赖其他 System Spec。
- 将新节点与新边增量写入 `openspec/spec-metadata/dependencies.json`；不得覆盖已有节点、边或 Spec。
- Verification 记录文档与代码证据及核验结论。`codeEvidence` 保存 Unity GUID、显示路径、脚本全文 SHA-256、入口行和具体大功能；功能描述必须说明代码做什么，不得只写脚本名或“脚本主要职责”。GUID/hash 未变化时复用，变化时只重读该脚本。
- `spec-review.json.implementationOutline` 用少量类似伪代码的句子概括核心数据、关键判断和主要调用方向，供关系图谱节点详情展示。
- Gap 只表示缺失的 System 依赖节点或契约，并与 open 依赖边一一对应。
- capability 已存在时先做语义比较：相同内容跳过，有变化时走已有 OpenSpec Change 流程，不直接重写正式 Spec。

## 工程模块拆分边界

本流程发现的真 Architecture/Plugin 进入 `project-tooling` 时，必须按 README/Wiki 的模块表与代码边界拆分。独立目录、公共接口或入口类型、可单独验证的职责共同成立时，一个核心模块对应一个工程能力条目；跨模块启动编排可以另建 System，但不能吞并 Resource、Event、Config、UI、Procedure 等模块。仅有历史文档而代码缺失时只记录 partial 候选和缺失事实。

## 输出

报告至少列出：候选架构资料、代码核验结果、新增或复用的 capability、跳过原因、冲突、缺失依赖，以及是否需要用户确认。
