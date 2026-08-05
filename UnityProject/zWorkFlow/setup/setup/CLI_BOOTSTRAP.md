# CLI Bootstrap

zWorkFlow 可以直接从 GitHub 下载，无需先手工复制 ZIP：

```sh
npx --yes github:Hubr1zz/zWorkFlow setup
```

也可指定项目目录：

```sh
npx --yes github:Hubr1zz/zWorkFlow setup /path/to/project
```

该命令要求 Node.js `>=20.19.0`，会验证 OpenSpec `>=1.6.0 <2.0.0`，缺失或过旧时自动安装兼容的 1.x，然后把干净分发内容复制到 `<项目>/zWorkFlow/`。已有 `zWorkFlow/` 时停止，不覆盖或合并。

CLI 负责可确定复现的下载、依赖验证与文件落位；它不会猜测项目架构、覆盖已有 Agent 配置或冒充 AI 完成冲突判断。下载完成后，按终端提示让项目 Agent 读取 `zWorkFlow/setup/SETUP_NEW_PROJECT.md`，完成项目事实发现、已有工作流共存分析和 Unity 工作台条件安装。

只读诊断当前运行时（不会安装或升级）：

```sh
npx --yes github:Hubr1zz/zWorkFlow doctor
```

若以后发布 npm 包，同一 CLI 可直接使用 `npx @hubr1zz/zworkflow setup`；在正式发布前只保证 GitHub 形式。
