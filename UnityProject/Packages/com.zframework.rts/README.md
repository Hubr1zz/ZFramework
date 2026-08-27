# ZFramework Roslyn Runtime Scripts

RTS 是面向 Agent 的 Unity 玩法实验层：Unity 只编译一次稳定宿主，玩法规则与状态按 Session 位于 `RTSWorkspace/Sessions/<Session>/Sources/`，由常驻 Roslyn 编译器在 Play Mode 中编译并原子换代。编译或激活失败时，最后健康代继续运行。

推荐代码结构是 **Data / Adaptor / View**：

- Data：唯一规则与状态所有者，纯 C#，不引用 Unity、ZFramework 或 RTS。
- Adaptor：分别衔接 RTS 能力和正式项目生命周期，只做翻译与对账。
- View：Unity 表现、Prefab/材质/音频引用和对象池，不拥有玩法规则。

同一份纯 Data 可同时被 RTS 与正式 Unity 代码编译。正式化按 `Assets/GameScripts/Generated/RTS/<Session>/ExportNNNN/` 输出增量模块；每个 Session 只归档自己的旧版，其他 Session 最新版可共存。工具不生成 Bootstrap Prefab、不自动修改场景；已有 Procedure、Module 或场景启动流程负责接入。

唯一菜单入口是 `ZFramework > RTS > Control Center`。主页签服务 Agent 工作流，手动恢复、正式化和文档分别位于独立页签。项目级配置在 `Project Settings > ZFramework RTS`。

发行 package 不携带示例 Session、玩法场景、截图或 PlayMode 测试程序集。首次创建 Session 时，外部工作区会生成在项目根目录的 `RTSWorkspace/Sessions/`；默认 Agent 验证只读取结构化运行数据，不截图。

首次使用见 [RTS 精简使用指南](Documentation~/RTS-QUICKSTART.md)，完整技术边界见 [RTS MVP 设计](Documentation~/RTS-MVP-PLAN.md)。
