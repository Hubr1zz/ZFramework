# zWorkFlow

zWorkFlow 是一个可部署到 Unity 和通用代码仓库的 Agent 工作流包。它提供统一的共享 skill、OpenSpec 生命周期、设计文档到项目实现的可追踪桥接，以及 Unity Agent Workbench。

当前发布：`0.2.4`（部署清单 `2026.08.04.4`）。

## 部署与升级

在本地部署包根目录执行：

```powershell
node .\bin\zworkflow.mjs setup <项目根目录>
```

或使用发布包：

```powershell
npx --yes github:Hubr1zz/zWorkFlow setup <项目根目录>
```

`setup` 会更新受管理的工作流程序内容；项目事实、个人偏好、`openspec/`、`.agent-memory/` 和现有草案/归档默认保留。Unity 项目还会将 Workbench 模板安装到配置的 Editor 目录，并记录安装状态以避免覆盖本地手工修改。

## 快速入口

- [工作流概览](./WORKFLOW_OVERVIEW.md)
- [快速开始](./WORKFLOW_QUICKSTART.md)
- [新项目部署](./setup/SETUP_NEW_PROJECT.md)
- [升级已有安装](./setup/UPGRADE_EXISTING_INSTALLATION.md)
- [Unity Workbench 集成契约](./setup/UNITY_WORKBENCH_INTEGRATION.md)

Unity Workbench 的“设计文档树”在顶部维护设计文档路径与实现后变更状态；“指令列表”集中展示设计导入、检查文档及时性、Spec 翻译、apply、sync 与归档等标准用户指令。
