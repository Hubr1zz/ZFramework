---
schemaVersion: 2
category: system
title: 工程能力目录
---

## ADDED Requirements

### Requirement: 共享工程能力目录
系统 MUST 以 Git 同步的单一权威目录记录 Plugin、Architecture、System 工程能力，并保存稳定 ID、说明、策略、约束、证据和依赖；可再生成的扫描指纹不得写入该权威目录。

#### Scenario: 团队成员读取相同目录
- **WHEN** 两名成员从同一提交打开项目
- **THEN** Agent 和 Workbench 必须读取同一份工程能力事实与策略

### Requirement: Plugin 判断依据
每个 Plugin 条目 MUST 支持用户填写判断依据。非空判断依据必须优先约束 Agent；为空时 Agent 才能根据项目现有调用风格和边界判断用法，并说明采用的证据。

#### Scenario: 用户填写 Odin 偏好
- **WHEN** 用户保存 Odin 的序列化和 Inspector 使用依据
- **THEN** 后续相关实现必须读取并遵守该依据

#### Scenario: 判断依据为空
- **WHEN** Plugin 的判断依据为空且实现任务命中其能力
- **THEN** Agent 必须核对现有代码风格与兼容性后自行判断，不得仅因插件存在而强制使用

### Requirement: Architecture 强制与锁定
Architecture 条目 MUST 固定为 required 和 locked。Agent 必须在修改 Architecture 条目或其公共实现前取得用户对本次修改的明确确认。

#### Scenario: Agent 尝试修改锁定架构
- **WHEN** 任务需要改变锁定 Architecture 的契约、实现或目录定义
- **THEN** Agent 必须停止该部分修改并请求用户确认

#### Scenario: 新实现可复用既有架构
- **WHEN** 已确认的 Architecture 能力适用于实现任务
- **THEN** Agent 必须优先使用该能力，不得无理由建立并行替代实现

### Requirement: System 与 Architecture 分类边界
系统 MUST 把可脱离当前游戏独立复用且功能完整的能力分类为 Architecture，把依赖具体游戏领域、GameManager、阶段或内容契约的能力分类为 System。

#### Scenario: 启动框架与 GameManager 共存
- **WHEN** 通用启动/资源加载框架把控制权交给项目 GameManager
- **THEN** 前者必须分类为 Architecture，后者必须分类为 System

### Requirement: 增量发现
setup MUST 从依赖清单、插件目录、程序集和代码引用发现 Plugin 候选，并通过来源指纹避免每次任务全量扫描。发现结果不得自动产生非空用户判断依据，也不得未经确认创建锁定 Architecture。

#### Scenario: 依赖来源未变化
- **WHEN** setup 或后续检查发现依赖来源指纹未变化
- **THEN** 系统必须复用已有目录摘要而不是重新全仓扫描

### Requirement: 工程能力工作台
Workbench MUST 在关系图谱按钮右侧提供工程能力入口，显示分类列表、依赖图和条目详情；Plugin 判断依据可以直接保存，Architecture 条目必须只读并显示确认门禁。

#### Scenario: 编辑 Plugin 判断依据
- **WHEN** 用户在 Plugin 详情输入判断依据并保存
- **THEN** Workbench 必须以 UTF-8 更新权威目录且刷新当前显示

#### Scenario: 查看 Architecture
- **WHEN** 用户选择 Architecture 条目
- **THEN** Workbench 必须显示 required/locked 状态、约束和“修改前需确认”提示，且不提供直接编辑入口

### Requirement: System 分类兼容
新生成的 OpenSpec metadata MUST 使用 `system` 表示项目系统；读取器和门禁必须继续把 legacy `architecture` 视为 System，直到数据经过显式迁移。

#### Scenario: 打开旧 Architecture Spec
- **WHEN** Workbench 读取 category 为 `architecture` 的旧 Spec 或 Change
- **THEN** 必须在 System 分类和图谱中正常显示且不改写原文件
