# zWorkFlow Portable Setup

可以直接从 GitHub 下载到当前项目：

```sh
npx --yes github:Hubr1zz/zWorkFlow setup
```

也可以把本目录以 `zWorkFlow/` 文件夹名原样放在新项目根目录；不要把其中内容直接摊平或手工覆盖项目文件。

然后对 Agent 说：

> 读取 `zWorkFlow/setup/SETUP_NEW_PROJECT.md` 并执行完整 setup。

命令行 bootstrap 会下载干净包并验证 OpenSpec；Agent setup 随后保护已有工作流、安装未占用核心、从项目事实生成内容 skills，并在适用的 Unity 项目中安装工作台。两阶段边界见 `setup/CLI_BOOTSTRAP.md`。安装源不包含原项目知识、个人资料、正式 Spec、Change 或设计源机器路径。
