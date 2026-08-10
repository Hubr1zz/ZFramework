## Why

zWorkFlow 目前只在 setup 阶段粗略发现依赖，功能实现时没有可被 Agent 和工作台共同消费的工程能力权威目录，因此“已安装插件”“团队偏好的使用方式”和“不得随意改动的可复用架构”容易混淆。现有 OpenSpec `architecture` 节点也实际承载了 GameManager、阶段编排等项目耦合系统，需要与真正可独立复用的 Architecture 能力分离。

## What Changes

- 新增 Git 同步的工程能力目录，记录 Plugin、Architecture、System 的说明、证据、能力、约束与依赖关系。
- Plugin 条目支持用户在工作台填写“判断依据”；为空时 Agent 才根据项目已有调用风格判断具体用法。
- Architecture 条目固定为强制使用并锁定；Agent 修改目录条目或对应公共实现前必须先取得用户确认。
- setup 初次建立目录并按依赖清单、插件目录、程序集和命名空间证据生成候选；后续只在来源指纹变化时增量复核。
- Workbench 在关系图谱按钮右侧增加“工程能力”入口，提供分类列表、详情、Plugin 判断依据编辑和能力依赖图。
- **BREAKING**：OpenSpec 展示分类把原“Architecture”重命名为“System”；新写入 metadata 使用 `system`，读取端继续兼容旧 `architecture`。

## Capabilities

### New Capabilities
- `engineering-capability-catalog`: 工程能力目录的权威数据、Agent 使用门禁、setup 发现与 Workbench 可视化编辑契约。

### Modified Capabilities

- 无现有正式 capability 的 Requirement 需要在本 Change 内直接修改；旧 `architecture` 数据通过兼容读取与后续显式 sync/migration 逐步收敛。

## Impact

- `.agents/skills/project-tooling/`、setup 清单和项目内容模板。
- OpenSpec 分类规则、提案/apply/sync/设计导入 Skill 与 metadata schema。
- Unity Workbench 的导航、数据模型、分类筛选、关系图谱和新增工程能力页面。
- 当前项目新增 Git 同步的工程能力目录；干净移植包只携带空目录与通用规则，不携带案例项目插件事实。
