## Context

运行时内容原先分散在 Settings 引用与 `Resources` 路径扫描中，调用者可在没有统一所有权的情况下自行加载表、扩展和字体。ZFramework 已提供资源模块与 Singleton 生命周期，因此项目侧只需要一个窄的内容源租约和一次显式 Bundle 传递。

## Goals / Non-Goals

**Goals:**

- 在 `GameApp.Entrance` 前加载、校验完整内容源并失败关闭。
- 让正式 Campaign 装配只读取同一个显式 Bundle，避免本地路径和隐藏发现。
- 保证并发 Prepare 共享一次加载，释放期间完成的请求不会泄漏资源。
- 保留既有 Candidate → Event Generation → Settlement Plan → Hunt Bundle 事务。

**Non-Goals:**

- 不修改 ZFramework 资源模块公开契约或 `GameApp.Entrance()` 签名。
- 不把 UI 事件送入 ActionQueue，不推进 Showdown 玩法。
- 不依赖 PRELOAD 标签，不增加 `Resources` 回退。

## Decisions

1. 使用独立 Manifest，而不是扩大 `PlayableBootstrapSettings` 的职责；Manifest 只描述资源闭包，Settings 继续保存运行配置与表现配置。
2. `PlayableContentSourceSystem` 是 ZFramework Singleton System；YooAsset Loader 为内部适配器，系统只持有一个 Manifest 根租约。
3. Procedure 完成资源准备后再进入 GameApp；Launcher 只有在 `GameApp.IsEntered` 为真时隐藏。
4. Manifest 先校验稳定 ID、schema、必需引用、重复表和营地扩展，再生成只读 Bundle。
5. 表 Runtime 的正式装配入口显式接收 Bundle；兼容 `Rebuild()` 仅使用当前已预载 Bundle，不恢复路径查找。
6. 内容资产移到 `AssetRaw/Configs` 与 `AssetRaw/Fonts` 并保留原 GUID，使既有序列化引用继续有效。

## Risks / Trade-offs

- 内容安装为进程级事务；一旦成功提交，后续非预期的 Unity 表现安装异常不支持用第二候选重试。当前 GameManager 在 inactive 对象上先配置，正常路径不会触发该分支。
- Manifest 根租约依赖 YooAsset 收集其序列化依赖；完整 Player 构建烟雾仍需在发布阶段验证。
- 旧测试若直接调用无参内容重建且没有准备 Bundle 将失败关闭，测试应显式提供 Bundle。
